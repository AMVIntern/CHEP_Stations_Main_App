using HalconDotNet;
using Microsoft.Extensions.Options;
using VisionApp.Infrastructure.Inspection.Models;
using VisionApp.Infrastructure.Inspection.Options;
using VisionApp.Infrastructure.Inspection.Pipeline;

namespace VisionApp.Infrastructure.Inspection.Steps.Post;

/// <summary>
/// Converts filtered YOLO detections into "Defect.Element" counts (RN.TLB1, TN.B1, ...).
/// Station-specific mapping comes from StationDefectAssignment options.
/// </summary>
public sealed class ElementAssignmentStep : IInspectionStep
{
	public string Name { get; }

	private readonly string _stationKey;
	private readonly string _inputKey;   // e.g. "YoloX_Filtered"
	private readonly string _outputKey;  // e.g. "ElementCounts"
	private readonly Station5DefectAssignmentStationOptions _opts;

	public ElementAssignmentStep(
		string name,
		string stationKey,
		string inputKey,
		string outputKey,
		IOptions<Station5DefectAssignmentStationOptions> opts)
	{
		Name = name;
		_stationKey = stationKey;
		_inputKey = inputKey;
		_outputKey = outputKey;
		_opts = opts.Value;
	}

	public Task ExecuteAsync(InspectionContext context, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		if (!context.Items.TryGetValue(_inputKey, out var obj) || obj is not YoloXOutput y)
			return Task.CompletedTask;

		var spec = _opts.TryGet(_stationKey);
		if (spec is null)
			return Task.CompletedTask;

		// Need image width for splits
		var img = context.WorkingImage;
		if (img is null || !img.IsInitialized())
			return Task.CompletedTask;

		HOperatorSet.GetImageSize(img, out var widthHT, out var heightHT);
		var imageWidth = (int)widthHT;
		if (imageWidth <= 0)
			return Task.CompletedTask;

		var cameraId = context.Key.CameraId;
		var triggerIndex = context.Key.Index;

		// Determine boards visible for this camera (2..4, sometimes 3)
		if (!spec.CameraBoards.TryGetValue(cameraId, out var boards) || boards.Count == 0)
			boards = new List<string> { "Unknown" };

		// Determine bearer for this trigger
		spec.BearerByTriggerIndex.TryGetValue(triggerIndex, out var bearer);
		bearer ??= "Unknown";

		var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		foreach (var det in y.Detections)
		{
			// 1) Rename label if needed for this station/trigger
			var effectiveLabel = ApplyRename(spec, det.Label, triggerIndex);

			// 2) Decide which element axis this defect targets
			var target = spec.DefectTarget.TryGetValue(effectiveLabel, out var t) ? t : "Board";

			// 3) Compute board by X split
			var cx = det.Rect.X + (det.Rect.Width * 0.5);
			var board = ResolveBoard(boards, imageWidth, cx);

			// 4) Choose element based on target
			var element = string.Equals(target, "Bearer", StringComparison.OrdinalIgnoreCase)
				? bearer
				: board;

			if (element.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
				continue;

			var key = $"{effectiveLabel}.{element}";

			if (!counts.TryAdd(key, 1))
				counts[key]++;
		}

		// Store for downstream (debugging / runner extraction / aggregator)
		context.Items[_outputKey] = new ElementCountsOutput(_stationKey, counts);

		return Task.CompletedTask;
	}

	private static string ApplyRename(StationSpec spec, string label, int triggerIndex)
	{
		foreach (var r in spec.RenameRules)
		{
			if (string.Equals(r.From, label, StringComparison.OrdinalIgnoreCase) &&
				r.TriggerIndexes.Contains(triggerIndex))
				return r.To;
		}
		return label;
	}

	private static string ResolveBoard(IReadOnlyList<string> boards, int imageWidth, double cx)
	{
		if (boards.Count == 1)
			return boards[0];

		var n = boards.Count;
		var seg = Math.Clamp((int)Math.Floor(cx / imageWidth * n), 0, n - 1);
		return boards[seg];
	}
}
