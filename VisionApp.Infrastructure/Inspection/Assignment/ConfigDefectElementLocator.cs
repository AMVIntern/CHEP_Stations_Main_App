using Microsoft.Extensions.Options;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inspection.DefectAssignment;

namespace VisionApp.Infrastructure.Inspection.Assignment;

public sealed class ConfigDefectElementLocator : IDefectElementLocator
{
	private readonly IReadOnlyDictionary<string, Station5DefectAssignmentStationOptions> _byStation;

	public ConfigDefectElementLocator(IOptions<Station5DefectAssignmentOptions> options)
	{
		_byStation = options.Value.Stations
			.Where(s => !string.IsNullOrWhiteSpace(s.StationKey))
			.ToDictionary(s => s.StationKey, StringComparer.OrdinalIgnoreCase);
	}

	public bool TryLocate(
		string stationKey,
		string cameraId,
		int triggerIndex,
		int imageWidth,
		double xCenter,
		out string boardElement,
		out string bearerElement)
	{
		boardElement = "Unknown";
		bearerElement = "Unknown";

		if (!_byStation.TryGetValue(stationKey, out var cfg))
			return false;

		// bearer
		if (cfg.BearerByTriggerIndex.TryGetValue(triggerIndex, out var b))
			bearerElement = b;

		// elements for this camera (2/3/4...)
		if (!cfg.CameraElements.TryGetValue(cameraId, out var elements) || elements.Length == 0)
			elements = new[] { "L", "R" }; // fallback

		// determine which segment X falls into
		var n = elements.Length;
		if (imageWidth <= 0)
			return true; // we at least resolved bearer; board remains Unknown

		var segWidth = imageWidth / (double)n;
		var seg = (int)Math.Floor(xCenter / segWidth);
		seg = Math.Clamp(seg, 0, n - 1);

		boardElement = elements[seg];
		return true;
	}
}
