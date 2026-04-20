using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inference.YOLOX.Options;
using VisionApp.Infrastructure.Inspection.Runners;

namespace VisionApp.Infrastructure.Inspection.Composition;

public static class InspectionBootstrapperV1
{
	public static IInspectionRunner Build(IServiceProvider sp)
	{
		var plan = sp.GetRequiredService<CapturePlan>();
		var routerLogger = sp.GetRequiredService<ILogger<TriggerKeyInspectionRouter>>();

		// Validate model keys exist (so typos fail fast)
		var yopt = sp.GetRequiredService<IOptions<YoloXOptions>>().Value;
		var modelKeys = new HashSet<string>(yopt.Models.Select(m => m.Key), StringComparer.OrdinalIgnoreCase);

		// Station builders (registered in DI)
		var builders = sp.GetServices<IStationPipelineBuilder>().ToList();
		if (builders.Count == 0)
			throw new InvalidOperationException("No IStationPipelineBuilder registered.");

		foreach (var b in builders)
		{
			if (!modelKeys.Contains(b.StationKey))
				throw new InvalidOperationException($"ModelKey '{b.StationKey}' not found in YoloXOptions.Models.");
		}

		var byKey = new Dictionary<TriggerKey, IInspectionRunner>();

		foreach (var key in plan.OrderedTriggers.Distinct())
		{
			IInspectionRunner? runner = null;

			foreach (var builder in builders)
			{
				runner = builder.TryBuildFor(key);
				if (runner != null)
					break;
			}

			if (runner != null)
				byKey[key] = runner;
		}

		var fallback = new NoInspectionRunner();

		return new TriggerKeyInspectionRouter(
			byKey: byKey,
			byCamera: new Dictionary<string, IInspectionRunner>(StringComparer.OrdinalIgnoreCase),
			fallback: fallback,
			logger: routerLogger);
	}

	private sealed class NoInspectionRunner : IInspectionRunner
	{
		public Task<InspectionResult> InspectAsync(FrameArrived frame, CancellationToken ct)
		{
			return Task.FromResult(new InspectionResult(
				frame.CycleId,
				frame.Key,
				Pass: false,
				Score: 0.0,
				Message: "No inspection configured for this TriggerKey",
				Metrics: null,
				Visuals: null,
				CompletedAt: DateTimeOffset.UtcNow));
		}
	}
}
