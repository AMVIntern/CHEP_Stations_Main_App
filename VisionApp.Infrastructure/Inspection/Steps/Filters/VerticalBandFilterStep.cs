using HalconDotNet;
using OpenCvSharp;
using VisionApp.Infrastructure.Inspection.Models;
using VisionApp.Infrastructure.Inspection.Pipeline;

namespace VisionApp.Infrastructure.Inspection.Steps.Filters;

public sealed class VerticalBandFilterStep : IInspectionStep
{
	public string Name { get; }

	private readonly string _inputKey;
	private readonly string _outputKey;
	private readonly IReadOnlyDictionary<int, VerticalBandRule> _rulesByIndex;
	private readonly bool _defaultKeepAll;

	public VerticalBandFilterStep(
		string name,
		string inputKey,
		string outputKey,
		IReadOnlyDictionary<int, VerticalBandRule> rulesByIndex,
		bool defaultKeepAll = true)
	{
		Name = name;
		_inputKey = inputKey;
		_outputKey = outputKey;
		_rulesByIndex = rulesByIndex;
		_defaultKeepAll = defaultKeepAll;
	}

	public Task ExecuteAsync(InspectionContext context, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		if (!context.Items.TryGetValue(_inputKey, out var obj) || obj is not YoloXOutput y)
			return Task.CompletedTask;

		// Prefer height from YoloXOutput (so this step doesn't depend on having an HImage)
		int imageHeight = y.ImageHeight;
		if (imageHeight <= 0)
		{
			var img = context.WorkingImage;
			if (img is null || !img.IsInitialized())
			{
				context.Items[_outputKey] = y; // pass through
				return Task.CompletedTask;
			}

			HOperatorSet.GetImageSize(img, out _, out var h);
			imageHeight = (int)h;
			if (imageHeight <= 0)
			{
				context.Items[_outputKey] = y;
				return Task.CompletedTask;
			}
		}

		var index = context.Key.Index;

		if (!_rulesByIndex.TryGetValue(index, out var rule))
		{
			if (_defaultKeepAll)
			{
				context.Items[_outputKey] = y;
				return Task.CompletedTask;
			}

			// default drop all
			context.Items[_outputKey] = new YoloXOutput(
				Passed: true,
				Confidence: 1.0,
				ImageWidth: y.ImageWidth,
				ImageHeight: imageHeight,
				ClassCounts: y.ClassCounts.Keys.ToDictionary(k => k, _ => 0),
				Detections: Array.Empty<YoloXDetection>());
			return Task.CompletedTask;
		}

		var topPct = Clamp01(rule.TopKeepHalfPct);
		var bottomPct = Clamp01(rule.BottomKeepHalfPct);

		int center = imageHeight / 2;

		int yMin = topPct >= 0.999999
			? 0
			: (int)Math.Round(center - (center * topPct));

		int yMax = bottomPct >= 0.999999
			? imageHeight
			: (int)Math.Round(center + (center * bottomPct));

		yMin = Math.Max(0, Math.Min(imageHeight, yMin));
		yMax = Math.Max(0, Math.Min(imageHeight, yMax));
		if (yMax < yMin) (yMin, yMax) = (yMax, yMin);

		var kept = y.Detections
			.Where(d =>
			{
				var r = d.Rect;
				double cy = r.Y + (r.Height * 0.5);
				return cy >= yMin && cy <= yMax;
			})
			.ToList();

		var newCounts = y.ClassCounts.Keys.ToDictionary(k => k, _ => 0);
		foreach (var d in kept)
		{
			if (newCounts.ContainsKey(d.Label)) newCounts[d.Label]++;
			else newCounts[d.Label] = 1;
		}

		var passed = kept.Count == 0;
		var conf = passed ? 1.0 : kept.Max(d => d.Probability);

		context.Items[_outputKey] = new YoloXOutput(
			Passed: passed,
			Confidence: conf,
			ImageWidth: y.ImageWidth,
			ImageHeight: imageHeight,
			ClassCounts: newCounts,
			Detections: kept);

		context.Items[$"{_outputKey}_Band"] = new
		{
			TriggerIndex = index,
			ImageHeight = imageHeight,
			Center = center,
			yMin,
			yMax,
			TopKeepHalfPct = topPct,
			BottomKeepHalfPct = bottomPct
		};

		return Task.CompletedTask;
	}

	private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}
