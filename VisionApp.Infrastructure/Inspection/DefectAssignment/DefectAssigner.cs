using VisionApp.Core.Domain;
using VisionApp.Infrastructure.Inspection.Models;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public static class DefectAssigner
{
	public static void ApplyDetections(
		Dictionary<string, int> counts,     // mutated
		string cameraId,
		int triggerIndex,
		int imageWidth,
		IReadOnlyList<YoloXDetection> detections,
		StationDefectAssignmentConfig station)
	{
		if (!station.CameraElements.TryGetValue(cameraId, out var elements) || elements.Length == 0)
			return;

		var bearer = station.BearerByTriggerIndex.TryGetValue(triggerIndex, out var b) ? b : "Unknown";

		foreach (var det in detections)
		{
			// 1) rename rule
			var effective = det.Label;
			if (det.Label == "PN" && triggerIndex >= 1 && triggerIndex <= station.PnToTn_MaxTriggerIndex)
				effective = "TN";

			// 2) bearer-based vs board-based
			bool bearerBased = (effective == "PN" || effective == "TN");

			string element;
			if (bearerBased)
			{
				element = bearer;
			}
			else
			{
				var cx = det.Rect.X + det.Rect.Width * 0.5;
				var idx = SegmentIndex(cx, imageWidth, elements.Length);
				element = elements[idx];
			}

			if (string.Equals(element, "Unknown", StringComparison.OrdinalIgnoreCase))
				continue;

			var key = $"{effective}.{element}";
			if (!counts.TryAdd(key, 1))
				counts[key]++;
		}
	}

	private static int SegmentIndex(double xCenter, int imageWidth, int segments)
	{
		if (segments <= 1) return 0;
		if (imageWidth <= 0) return 0;

		var t = xCenter / imageWidth; // 0..1
		var idx = (int)Math.Floor(t * segments);

		if (idx < 0) idx = 0;
		if (idx >= segments) idx = segments - 1;
		return idx;
	}
}

public sealed class StationDefectAssignmentConfig
{
	public required string StationKey { get; init; }

	// triggerIndex -> bearer label (eg 1->B1,3->B2,5->B3)
	public required Dictionary<int, string> BearerByTriggerIndex { get; init; }

	// cameraId -> elements (2/3/4 supported)
	public required Dictionary<string, string[]> CameraElements { get; init; }

	// station-specific rename rule: PN->TN for triggers 1..N
	public int PnToTn_MaxTriggerIndex { get; init; } = 4;
}
