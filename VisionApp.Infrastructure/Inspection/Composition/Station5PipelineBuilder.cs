using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inference.YOLOX.Abstractions;
using VisionApp.Infrastructure.Inspection.DefectAssignment;
using VisionApp.Infrastructure.Inspection.Pipeline;
using VisionApp.Infrastructure.Inspection.Runners;
using VisionApp.Infrastructure.Inspection.Steps;
using VisionApp.Infrastructure.Inspection.Steps.Filters;

namespace VisionApp.Infrastructure.Inspection.Composition;

public sealed class Station5PipelineBuilder : IStationPipelineBuilder
{
	public string StationKey => "Station5";
	private readonly ICameraStationResolver _stations;
	private readonly IOptionsMonitor<Station5DefectAssignmentOptions> _optionsMonitor;

	// Station 5 labels (set these correctly for the Station5 model)
	private static readonly string[] Labels = { "RN", "PN", "PL", "ST", "FN" };

	private const float DefaultNms = 0.45f;

	private readonly IYoloXEngine _engine;
	private readonly ILogger<SequentialInspectionRunner> _seqLogger;
	private readonly ILogger<TraceStep> _traceLogger;

	// Built from appsettings at startup
	private readonly IReadOnlyDictionary<int, VerticalBandRule> _verticalBandRules;

	public Station5PipelineBuilder(
		ICameraStationResolver stations,
		IYoloXEngine engine,
		IOptionsMonitor<Station5DefectAssignmentOptions> optionsMonitor,
		ILogger<SequentialInspectionRunner> seqLogger,
		ILogger<TraceStep> traceLogger)
	{
		_stations = stations;
		_engine = engine;
		_optionsMonitor = optionsMonitor;
		_seqLogger = seqLogger;
		_traceLogger = traceLogger;

		var opts = optionsMonitor.CurrentValue;
		_verticalBandRules = opts.VerticalBandRules
			.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToRule());
	}

	public IInspectionRunner? TryBuildFor(TriggerKey key)
	{
		// Only handle Station 5 cameras
		var station = _stations.TryGetStationKey(key.CameraId);
		if (!string.Equals(station, StationKey, StringComparison.OrdinalIgnoreCase))
			return null;

		// Route by trigger index ("frame number" in your old app, now TriggerKey.Index)
		return key.Index switch
		{
			1 => BuildTrigger1(key),
			2 => BuildTrigger2(key),
			3 => BuildTrigger3(key),
			4 => BuildTrigger4(key),
			5 => BuildTrigger5(key),
			_ => BuildDefault(key),
		};
	}

	// --------------------------
	// Recipe-like pipelines below
	// --------------------------

	private IInspectionRunner BuildTrigger1(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		// Example: Trigger 1 might need only YOLOX + decision
		var steps = new IInspectionStep[]
		{
			Trace($"S5[{key.Index}] Start"),

			// Yolo object detection (RN, PN, PL etc)
			YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),

			Trace($"S5[{key.Index}] After YOLOX"),

			// Duplicate overlap filter
			new VerticalBandFilterStep(
				name: "VerticalBandFilter",
				inputKey: "YoloX",
				outputKey: "YoloX_Filtered",
				rulesByIndex: _verticalBandRules,
				defaultKeepAll: true),

			Decide(fromOutput: "YoloX_Filtered"),
		};

		return Seq(steps);
	}

	private IInspectionRunner BuildTrigger2(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		// Example: Trigger 2 might add extra steps later
		var steps = new IInspectionStep[]
		{
			Trace($"S5[{key.Index}] Start"),

            // Later you might add something like:
            // new HalconStapleCheckStep(...),
            // new CropRoiStep(...),

            YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),

			Decide(fromOutput: "YoloX"),
		};

		return Seq(steps);
	}

	private IInspectionRunner BuildTrigger3(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		// Example: Trigger 3 could be "same as trigger2"
		var steps = new IInspectionStep[]
		{
			Trace($"S5[{key.Index}] Start"),
			YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),
			Decide(fromOutput: "YoloX"),
		};

		return Seq(steps);
	}

	private IInspectionRunner BuildTrigger4(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		// Per-class thresholds now apply uniformly to all triggers (no trigger-specific overrides)

		var steps = new IInspectionStep[]
		{
			Trace($"S5[{key.Index}] Start"),

			YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),

            // Maybe a second model later, or a HALCON measurement
            // new SomeMeasurementStep(...),

            Decide(fromOutput: "YoloX"),
		};

		return Seq(steps);
	}

	private IInspectionRunner BuildTrigger5(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		// Per-class thresholds now apply uniformly to all triggers (no trigger-specific overrides)

		var steps = new IInspectionStep[]
		{
			Trace($"S5[{key.Index}] Start"),

			YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),

            // Maybe a second model later, or a HALCON measurement
            // new SomeMeasurementStep(...),

            Decide(fromOutput: "YoloX"),
		};

		return Seq(steps);
	}

	private IInspectionRunner BuildDefault(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		var steps = new IInspectionStep[]
		{
			Trace($"S5[{key.Index}] Default"),
			YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),
			Decide(fromOutput: "YoloX"),
		};

		return Seq(steps);
	}

	// --------------------------
	// Tiny helpers (reads nicely)
	// --------------------------

	private SequentialInspectionRunner Seq(IEnumerable<IInspectionStep> steps)
		=> new SequentialInspectionRunner(steps, _seqLogger);

	private TraceStep Trace(string name)
		=> new TraceStep(name, _traceLogger);

	// YOLOX step with Station5 model 1
	private YoloXStep YoloX1(string stepName, IReadOnlyDictionary<string, double> classThresholds, double defaultThreshold)
	{
		var opts = _optionsMonitor.CurrentValue;
		return new YoloXStep(
			name: stepName,
			engine: _engine,
			modelKey: StationKey,
			classThresholds: classThresholds,
			defaultThreshold: defaultThreshold,
			nmsThreshold: DefaultNms,
			classLabels: opts.ClassLabels);
	}

	private DecisionStep Decide(string fromOutput)
		=> new DecisionStep(fromOutput);
}


