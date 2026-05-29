using HalconDotNet;
using OpenCvSharp;
using VisionApp.Inference.YoloX.Models;
using VisionApp.Inference.YoloX.Utils;
using VisionApp.Infrastructure.Inference.YOLOX.Abstractions;
using VisionApp.Infrastructure.Inspection.Models;
using VisionApp.Infrastructure.Inspection.Pipeline;

namespace VisionApp.Infrastructure.Inspection.Steps;

public sealed class YoloXStep : IInspectionStep
{
	public string Name { get; }

	private readonly IYoloXEngine _engine;
	private readonly string _modelKey;
	private readonly IReadOnlyDictionary<string, double> _classThresholds;
	private readonly double _defaultThreshold;
	private readonly Func<(IReadOnlyDictionary<string, double> ClassThresholds, double DefaultThreshold)>? _thresholdProvider;
	private readonly float _nmsThreshold;
	private readonly string[] _classLabels;

	/// <summary>
	/// Labels that are stripped from the output entirely — no bounding box, no pass/fail
	/// contribution, no counts. Intended for "background" or "GOOD" model classes.
	/// </summary>
	private readonly HashSet<string> _ignoreLabels;

	public YoloXStep(
		string name,
		IYoloXEngine engine,
		string modelKey,
		IReadOnlyDictionary<string, double> classThresholds,
		double defaultThreshold,
		float nmsThreshold,
		string[] classLabels,
		string[]? ignoreLabels = null,
		Func<(IReadOnlyDictionary<string, double> ClassThresholds, double DefaultThreshold)>? thresholdProvider = null)
	{
		Name = name;
		_engine = engine;
		_modelKey = modelKey;
		_classThresholds = classThresholds;
		_defaultThreshold = defaultThreshold;
		_thresholdProvider = thresholdProvider;
		_nmsThreshold = nmsThreshold;
		_classLabels = classLabels;
		_ignoreLabels = ignoreLabels?.Length > 0
			? new HashSet<string>(ignoreLabels, StringComparer.OrdinalIgnoreCase)
			: new HashSet<string>();
	}

	public async Task ExecuteAsync(InspectionContext context, CancellationToken ct)
	{
		using var stepImage = context.WorkingImage?.IsInitialized() == true
			? context.WorkingImage.CopyImage()
			: new HImage();

		if (!stepImage.IsInitialized())
		{
			context.Items[Name] = new YoloXOutput(
				Passed: false,
				Confidence: 0,
				ImageWidth: 0,
				ImageHeight: 0,
				ClassCounts: new Dictionary<string, int>(),
				Detections: Array.Empty<YoloXDetection>());
			return;
		}

		using var mat = ImageUtils.HImageToMatBGR(stepImage);

		List<PredictionObject> raw = await _engine.InferAsync(
			modelKey: _modelKey,
			imageBgr: mat,
			ct: ct).ConfigureAwait(false);

		var (activeThresholds, activeDefaultThreshold) = _thresholdProvider?.Invoke()
			?? (_classThresholds, _defaultThreshold);

		var filtered = raw.Where(p =>
		{
			var label = _classLabels[p.Label];
			var threshold = activeThresholds.TryGetValue(label, out var t) ? t : activeDefaultThreshold;
			return p.Probability >= threshold;
		}).ToList();

		var counts = _classLabels.ToDictionary(x => x, _ => 0);
		var dets = new List<YoloXDetection>(filtered.Count);

		foreach (var p in filtered)
		{
			var label = _classLabels[p.Label];

			// Ignored labels are stripped completely: no box, no count, no pass/fail impact
			if (_ignoreLabels.Count > 0 && _ignoreLabels.Contains(label))
				continue;

			if (counts.ContainsKey(label)) counts[label]++;
			else counts[label] = 1;

			dets.Add(new YoloXDetection(
				Label: label,
				Rect: new Rect(
					(int)Math.Floor(p.Rect.X),
					(int)Math.Floor(p.Rect.Y),
					(int)Math.Ceiling(p.Rect.Width),
					(int)Math.Ceiling(p.Rect.Height)),
				Probability: p.Probability
			));
		}

		var passed = dets.Count == 0;
		var conf = passed ? 1.0 : dets.Max(d => d.Probability);

		context.Items[Name] = new YoloXOutput(
			Passed: passed,
			Confidence: conf,
			ImageWidth: mat.Width,
			ImageHeight: mat.Height,
			ClassCounts: counts,
			Detections: dets
		);
	}
}
