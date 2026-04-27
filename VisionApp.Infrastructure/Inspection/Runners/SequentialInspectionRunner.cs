using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inspection.Models;
using VisionApp.Infrastructure.Inspection.Pipeline;
using VisionApp.Infrastructure.Inspection.Steps;

namespace VisionApp.Infrastructure.Inspection.Runners;

public sealed class SequentialInspectionRunner : IInspectionRunner
{
    private readonly IReadOnlyList<IInspectionStep> _steps;
    private readonly ILogger<SequentialInspectionRunner> _logger;

    public SequentialInspectionRunner(IEnumerable<IInspectionStep> steps, ILogger<SequentialInspectionRunner> logger)
    {
        _steps = steps.ToList();
        _logger = logger;
    }

	public async Task<InspectionResult> InspectAsync(FrameArrived frame, CancellationToken ct)
	{
		if (frame.Image is null || !frame.Image.IsInitialized())
		{
			return new InspectionResult(
				frame.CycleId,
				frame.Key,
				Pass: false,
				Score: 0.0,
				Message: "Image not initialized",
				Metrics: null,
				Visuals: null,
				CompletedAt: DateTimeOffset.UtcNow);
		}

		try
		{
			using var ctx = new InspectionContext(frame);

			foreach (var step in _steps)
				await step.ExecuteAsync(ctx, ct).ConfigureAwait(false);

			// Prefer filtered visuals
			var visuals = ExtractVisuals(ctx);

			// Prefer dimensions from filtered output, then base output
			int w = 0, h = 0;
			if (ctx.TryGet<YoloXOutput>("YoloX_Filtered", out var yf))
			{
				w = yf.ImageWidth;
				h = yf.ImageHeight;
			}
			else if (ctx.TryGet<YoloXOutput>("YoloX", out var y))
			{
				w = y.ImageWidth;
				h = y.ImageHeight;
			}

			if (ctx.Items.TryGetValue(InspectionKeys.Final, out var obj) && obj is FinalDecision final)
			{
				return new InspectionResult(
					frame.CycleId,
					frame.Key,
					Pass: final.Pass,
					Score: final.Score,
					Message: final.Message,
					Metrics: null,
					CompletedAt: DateTimeOffset.UtcNow,
					ImageWidth: w,
					ImageHeight: h,
					Visuals: visuals);
			}

			return new InspectionResult(
				frame.CycleId,
				frame.Key,
				Pass: false,
				Score: 0.0,
				Message: "No DecisionStep output found (__final missing).",
				Metrics: null,
				CompletedAt: DateTimeOffset.UtcNow,
				ImageWidth: w,
				ImageHeight: h,
				Visuals: visuals);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Inspection pipeline failed for {Key}", frame.Key);
			return new InspectionResult(
				frame.CycleId,
				frame.Key,
				Pass: false,
				Score: 0.0,
				Message: ex.Message,
				Metrics: null,
				Visuals: null,
				CompletedAt: DateTimeOffset.UtcNow);
		}
	}

	private static InspectionVisuals? ExtractVisuals(InspectionContext ctx)
	{
		var dict = new Dictionary<string, IReadOnlyList<OverlayBox>>(StringComparer.OrdinalIgnoreCase);

		// Primary filtered output
		if (ctx.Items.TryGetValue("YoloX_Filtered", out var filteredObj) && filteredObj is YoloXOutput fy)
		{
			var boxes = fy.Detections
				.Select(d => new OverlayBox(
					Label: d.Label,
					Confidence: d.Probability,
					Rect: new RectD(d.Rect.X, d.Rect.Y, d.Rect.Width, d.Rect.Height)))
				.ToList();

			if (boxes.Count > 0)
				dict["YoloX_Filtered"] = boxes; // normalize key so UI doesn't have to change
		}

		// Secondary filtered output (if configured).
		if (ctx.Items.TryGetValue("YoloX2_Filtered", out var filteredObj2) && filteredObj2 is YoloXOutput fy2)
		{
			var boxes = fy2.Detections
				.Select(d => new OverlayBox(
					Label: d.Label,
					Confidence: d.Probability,
					Rect: new RectD(d.Rect.X, d.Rect.Y, d.Rect.Width, d.Rect.Height)))
				.ToList();

			if (boxes.Count > 0)
				dict["YoloX2_Filtered"] = boxes;
		}

		// Fallback: raw
		if (ctx.Items.TryGetValue("YoloX", out var rawObj) && rawObj is YoloXOutput ry)
		{
			var boxes = ry.Detections
				.Select(d => new OverlayBox(
					Label: d.Label,
					Confidence: d.Probability,
					Rect: new RectD(d.Rect.X, d.Rect.Y, d.Rect.Width, d.Rect.Height)))
				.ToList();

			if (boxes.Count > 0)
				dict["YoloX"] = boxes;
		}

		return dict.Count == 0 ? null : new InspectionVisuals(dict);
	}
	private static IReadOnlyDictionary<string, double>? ExtractMetrics(InspectionContext ctx)
	{
		// Look for per-frame element counts
		if (ctx.Items.TryGetValue("ElementCounts", out var obj) && obj is ElementCountsOutput eco)
		{
			// Convert int counts to double for Metrics
			return eco.Counts.ToDictionary(k => k.Key, v => (double)v.Value, StringComparer.OrdinalIgnoreCase);
		}

		return null;
	}
}