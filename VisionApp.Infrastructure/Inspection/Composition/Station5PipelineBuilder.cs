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
	public string StationKey => _stationKey;
	private readonly ICameraStationResolver _stations;
	private readonly IOptionsMonitor<Station5DefectAssignmentOptions> _optionsMonitor;
	private readonly string _stationKey;

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
		_stationKey = string.IsNullOrWhiteSpace(opts.StationKey) ? "Station5" : opts.StationKey.Trim();
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
		return Seq(BuildSteps(key.Index, opts));
	}

	private IInspectionRunner BuildTrigger2(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		return Seq(BuildSteps(key.Index, opts));
	}

	private IInspectionRunner BuildTrigger3(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		return Seq(BuildSteps(key.Index, opts));
	}

	private IInspectionRunner BuildTrigger4(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		return Seq(BuildSteps(key.Index, opts));
	}

	private IInspectionRunner BuildTrigger5(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		return Seq(BuildSteps(key.Index, opts));
	}

	private IInspectionRunner BuildDefault(TriggerKey key)
	{
		var opts = _optionsMonitor.CurrentValue;
		return Seq(BuildSteps(key.Index, opts));
	}

	private IInspectionStep[] BuildSteps(int triggerIndex, Station5DefectAssignmentOptions opts)
	{
		var sec = opts.SecondaryModel;
		if (sec is { Key.Length: > 0, ClassLabels.Length: > 0 })
		{
			return new IInspectionStep[]
			{
				Trace($"S5[{triggerIndex}] Start"),
				new ParallelGroupStep("ModelInference", new IInspectionStep[]
				{
					YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),
					YoloX2("YoloX2", sec),
				}),
				new ParallelGroupStep("BandFilter", new IInspectionStep[]
				{
					VerticalBandFilter("VerticalBandFilter", "YoloX", "YoloX_Filtered"),
					VerticalBandFilter("VerticalBandFilter2", "YoloX2", "YoloX2_Filtered"),
				}),
				Decide("YoloX_Filtered", "YoloX2_Filtered"),
			};
		}

		return new IInspectionStep[]
		{
			Trace($"S5[{triggerIndex}] Start"),
			YoloX1("YoloX", classThresholds: opts.ClassThresholds, defaultThreshold: opts.DefaultThreshold),
			VerticalBandFilter("VerticalBandFilter", "YoloX", "YoloX_Filtered"),
			Decide("YoloX_Filtered"),
		};
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

	private YoloXStep YoloX2(string stepName, Station5SecondaryModelOptions sec)
	{
		var opts = _optionsMonitor.CurrentValue;
		return new YoloXStep(
			name: stepName,
			engine: _engine,
			modelKey: sec.Key,
			classThresholds: sec.ClassThresholds,
			defaultThreshold: opts.DefaultThreshold,
			nmsThreshold: DefaultNms,
			classLabels: sec.ClassLabels);
	}

	private VerticalBandFilterStep VerticalBandFilter(string stepName, string inputKey, string outputKey)
		=> new(
			name: stepName,
			inputKey: inputKey,
			outputKey: outputKey,
			rulesByIndex: _verticalBandRules,
			defaultKeepAll: true);

	private DecisionStep Decide(string fromOutput)
		=> new DecisionStep(fromOutput);

	private DecisionStep Decide(params string[] fromOutputs)
		=> new DecisionStep(fromOutputs);
}


