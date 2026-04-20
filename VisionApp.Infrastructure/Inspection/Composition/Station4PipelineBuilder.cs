using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inference.YOLOX.Abstractions;
using VisionApp.Infrastructure.Inspection.DefectAssignment;
using VisionApp.Infrastructure.Inspection.Pipeline;
using VisionApp.Infrastructure.Inspection.Runners;
using VisionApp.Infrastructure.Inspection.Steps;

namespace VisionApp.Infrastructure.Inspection.Composition;

public sealed class Station4PipelineBuilder : IStationPipelineBuilder
{
	public string StationKey => "Station4";
	private readonly ICameraStationResolver _stations;
	private readonly IOptionsMonitor<Station4DefectAssignmentOptions> _optionsMonitor;

	private const float DefaultNms = 0.45f;

	private readonly IYoloXEngine _engine;
	private readonly ILogger<SequentialInspectionRunner> _seqLogger;
	private readonly ILogger<TraceStep> _traceLogger;

	public Station4PipelineBuilder(
		ICameraStationResolver stations,
		IYoloXEngine engine,
		IOptionsMonitor<Station4DefectAssignmentOptions> optionsMonitor,
		ILogger<SequentialInspectionRunner> seqLogger,
		ILogger<TraceStep> traceLogger)
	{
		_stations = stations;
		_engine = engine;
		_optionsMonitor = optionsMonitor;
		_seqLogger = seqLogger;
		_traceLogger = traceLogger;
	}

	public IInspectionRunner? TryBuildFor(TriggerKey key)
	{
		var station = _stations.TryGetStationKey(key.CameraId);
		if (!string.Equals(station, StationKey, StringComparison.OrdinalIgnoreCase))
			return null;

		return key.Index switch
		{
			1 => BuildTrigger1(key),
			2 => BuildTrigger2(key),
			3 => BuildTrigger3(key),
			4 => BuildTrigger4(key),
			_ => BuildDefault(key),
		};
	}

	private IInspectionRunner BuildTrigger1(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		var steps = new IInspectionStep[]
		{
			Trace($"S4[{key.Index}] Start"),
			YoloX("YoloX", opts.ClassThresholds, opts.DefaultThreshold),
			Decide("YoloX"),
		};
		return Seq(steps);
	}

	private IInspectionRunner BuildTrigger2(TriggerKey key) => BuildTrigger1(key);
	private IInspectionRunner BuildTrigger3(TriggerKey key) => BuildTrigger1(key);
	private IInspectionRunner BuildTrigger4(TriggerKey key) => BuildTrigger1(key);

	private IInspectionRunner BuildDefault(TriggerKey key) => BuildTrigger1(key);

	private SequentialInspectionRunner Seq(IEnumerable<IInspectionStep> steps)
		=> new SequentialInspectionRunner(steps, _seqLogger);

	private TraceStep Trace(string name)
		=> new TraceStep(name, _traceLogger);

	private YoloXStep YoloX(string stepName, IReadOnlyDictionary<string, double> classThresholds, double defaultThreshold)
	{
		var opts = _optionsMonitor.CurrentValue;
		return new YoloXStep(
			name: stepName,
			engine: _engine,
			modelKey: StationKey,
			classThresholds: classThresholds,
			defaultThreshold: defaultThreshold,
			nmsThreshold: DefaultNms,
			classLabels: opts.ClassLabels,
			ignoreLabels: opts.IgnoreLabels);
	}

	private DecisionStep Decide(string fromOutput)
		=> new DecisionStep(fromOutput);

	private static string[] NormalizeDistinctOrdered(IEnumerable<string> values)
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<string>();

		foreach (var v in values ?? Array.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(v))
				continue;

			var s = v.Trim();
			if (set.Add(s))
				list.Add(s);
		}

		return list.ToArray();
	}
}
