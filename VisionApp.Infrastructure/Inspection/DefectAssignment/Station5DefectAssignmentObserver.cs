using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inspection.Composition;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public sealed class Station5DefectAssignmentObserver : IInspectionObserver
{
	private readonly StationDefectAccumulator _acc;
	private readonly ICameraStationResolver _stations;
	private readonly CapturePlan _plan;
	private readonly Station5DefectAssignmentOptions _opts;
	private readonly IShiftResolver _shift;
	private readonly IStationDefectRowSink _sink;
	private readonly ILogger<Station5DefectAssignmentObserver> _logger;

	// stationKey -> config
	private readonly Dictionary<string, Station5DefectAssignmentStationOptions> _cfgByStation;

	// stationKey -> required end triggers for that station
	private readonly Dictionary<string, HashSet<TriggerKey>> _stationEndTriggers;

	public Station5DefectAssignmentObserver(
		StationDefectAccumulator acc,
		ICameraStationResolver stations,
		CapturePlan plan,
		IOptions<Station5DefectAssignmentOptions> options,
		IShiftResolver shift,
		IStationDefectRowSink sink,
		ILogger<Station5DefectAssignmentObserver> logger)
	{
		_acc = acc;
		_stations = stations;
		_plan = plan;
		_opts = options.Value;
		_shift = shift;
		_sink = sink;
		_logger = logger;

		_cfgByStation = _opts.Stations
			.Where(s => !string.IsNullOrWhiteSpace(s.StationKey))
			.ToDictionary(s => s.StationKey, s => NormalizeStationOptions(s), StringComparer.OrdinalIgnoreCase);

		_stationEndTriggers = BuildStationEndTriggers(plan, stations);
	}

	public async Task OnInspectionCompletedAsync(InspectionResult result, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		var cameraId = result.Key.CameraId;

		// 1) Resolve station
		var stationKey = _stations.TryGetStationKey(cameraId);
		if (string.IsNullOrWhiteSpace(stationKey))
			return;

		if (!_cfgByStation.TryGetValue(stationKey, out var cfg))
			return;

		// 2) End-trigger tracking must ALWAYS happen (even if no detections)
		if (_stationEndTriggers.TryGetValue(stationKey, out var requiredEnd) && requiredEnd.Count > 0)
		{
			if (requiredEnd.Contains(result.Key))
				_acc.MarkEndSeen(result.CycleId, stationKey, result.Key);
		}

		// 3) Aggregate only if we actually have boxes
		if (TryGetBoxes(result, _opts.InputOutputKey, out var boxes) && boxes.Count > 0)
		{
			var perFrameCounts = AssignCounts(
				cfg: cfg,
				labelToGroup: _opts.LabelToGroup,
				cameraId: cameraId,
				triggerIndex: result.Key.Index,
				imageWidth: result.ImageWidth,
				boxes: boxes);

			if (perFrameCounts.Count > 0)
				_acc.AddCounts(result.CycleId, stationKey, perFrameCounts);
		}

		// 4) Completion check (again: independent from boxes)
		if (_stationEndTriggers.TryGetValue(stationKey, out requiredEnd) && requiredEnd.Count > 0)
		{
			if (_acc.TryComplete(result.CycleId, stationKey, requiredEnd, out var finalCounts))
			{
				var (shift, shiftDate, calendarDate) = _shift.Resolve(result.CompletedAt);
				var local = TimeZoneInfo.ConvertTime(result.CompletedAt, TimeZoneInfo.Local);

				var row = new StationDefectRow(
					CycleId: result.CycleId,
					StationKey: stationKey,
					Date: shiftDate,
					CalendarDate: calendarDate,
					Timestamp: TimeOnly.FromDateTime(local.DateTime),
					Shift: shift,
					Counts: finalCounts);

				await _sink.WriteAsync(row, ct).ConfigureAwait(false);
			}
		}
	}

	private static bool TryGetBoxes(InspectionResult result, string preferredKey, out IReadOnlyList<OverlayBox> boxes)
	{
		boxes = Array.Empty<OverlayBox>();

		var map = result.Visuals?.BoxesByStep;
		if (map is null || map.Count == 0)
			return false;

		// Prefer configured key
		if (!string.IsNullOrWhiteSpace(preferredKey) && map.TryGetValue(preferredKey, out boxes))
			return true;

		// Optional: fallback if you want (safe even with Option A)
		if (map.TryGetValue("YoloX_Filtered", out boxes))
			return true;
		if (map.TryGetValue("YoloX", out boxes))
			return true;

		return false;
	}


	private static Dictionary<string, int> AssignCounts(
		Station5DefectAssignmentStationOptions cfg,
		IReadOnlyDictionary<string, string> labelToGroup,
		string cameraId,
		int triggerIndex,
		int imageWidth,
		IReadOnlyList<OverlayBox> boxes)
	{
		var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		if (!cfg.CameraElements.TryGetValue(cameraId, out var elements) || elements.Length == 0)
			return counts;

		cfg.BearerByTriggerIndex.TryGetValue(triggerIndex, out var bearer);
		bearer ??= "Unknown";

		foreach (var b in boxes)
		{
			var label = b.Label;
			if (labelToGroup != null && labelToGroup.TryGetValue(label, out var mapped))
				label = mapped;

			if (string.Equals(label, "PN", StringComparison.OrdinalIgnoreCase) &&
				cfg.PnToTn_MaxTriggerIndex is { Length: > 0 } &&
				Array.IndexOf(cfg.PnToTn_MaxTriggerIndex, triggerIndex) >= 0)
			{
				label = "TN";
			}

			var bearerBased = string.Equals(label, "PN", StringComparison.OrdinalIgnoreCase) ||
							  string.Equals(label, "TN", StringComparison.OrdinalIgnoreCase);

			string element;

			if (bearerBased)
			{
				element = bearer;
			}
			else
			{
				// If we don't know imageWidth, we can't split into board segments safely
				if (imageWidth <= 0)
					continue;

				var cx = b.Rect.X + (b.Rect.Width * 0.5);
				var segIdx = SegmentIndex(cx, imageWidth, elements.Length);
				element = elements[segIdx];
			}

			if (string.Equals(element, "Unknown", StringComparison.OrdinalIgnoreCase))
				continue;

			var key = $"{label}.{element}";
			if (!counts.TryAdd(key, 1))
				counts[key]++;
		}

		return counts;
	}


	private static int SegmentIndex(double xCenter, int imageWidth, int segments)
	{
		if (segments <= 1) return 0;
		if (imageWidth <= 0) return 0;

		var t = xCenter / imageWidth; // 0..1-ish
		var idx = (int)Math.Floor(t * segments);

		if (idx < 0) idx = 0;
		if (idx >= segments) idx = segments - 1;
		return idx;
	}

	private static Dictionary<string, HashSet<TriggerKey>> BuildStationEndTriggers(CapturePlan plan, ICameraStationResolver stations)
	{
		var dict = new Dictionary<string, HashSet<TriggerKey>>(StringComparer.OrdinalIgnoreCase);

		foreach (var k in plan.EndTriggers)
		{
			var station = stations.TryGetStationKey(k.CameraId);
			if (string.IsNullOrWhiteSpace(station))
				continue;

			if (!dict.TryGetValue(station, out var set))
			{
				set = new HashSet<TriggerKey>();
				dict[station] = set;
			}

			set.Add(k);
		}

		return dict;
	}

	private static Station5DefectAssignmentStationOptions NormalizeStationOptions(Station5DefectAssignmentStationOptions s)
	{
		// make camera dictionary case-insensitive
		var cam = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in s.CameraElements)
			cam[kvp.Key] = kvp.Value;

		return new Station5DefectAssignmentStationOptions
		{
			StationKey = s.StationKey,
			BearerByTriggerIndex = new Dictionary<int, string>(s.BearerByTriggerIndex),
			CameraElements = cam,
			PnToTn_MaxTriggerIndex = s.PnToTn_MaxTriggerIndex
		};
	}
}
