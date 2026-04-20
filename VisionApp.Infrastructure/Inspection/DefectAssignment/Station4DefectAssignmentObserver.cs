using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inspection.Composition;
using VisionApp.Infrastructure.PlcOutbound;
using VisionApp.Infrastructure.Sinks;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

/// <summary>
/// Station4 defect assignment:
/// - element is derived from trigger index (1=B1, 2=B1-B2, 3=B2-B3, 4=B3)
/// - element is camera-prefixed (Cam1_B1, Cam2_B3, ...)
/// - writes a StationDefectRow to the Station4 CSV sink when Station4 end triggers are complete
/// </summary>
public sealed class Station4DefectAssignmentObserver : IInspectionObserver
{
	private readonly StationDefectAccumulator _acc;
	private readonly ICameraStationResolver _stations;
	private readonly CapturePlan _plan;
	private readonly Station4DefectAssignmentOptions _opts;
	private readonly IShiftResolver _shift;
	private readonly Station4DefectReportCsvSink _sink;
	private readonly PlcStationDefectBoolSink _plcSink;
	private readonly ILogger<Station4DefectAssignmentObserver> _logger;

	private readonly HashSet<TriggerKey> _requiredEnd;

	public Station4DefectAssignmentObserver(
		StationDefectAccumulator acc,
		ICameraStationResolver stations,
		CapturePlan plan,
		IOptions<Station4DefectAssignmentOptions> options,
		IShiftResolver shift,
		Station4DefectReportCsvSink sink,
		PlcStationDefectBoolSink plcSink,
		ILogger<Station4DefectAssignmentObserver> logger)
	{
		_acc = acc;
		_stations = stations;
		_plan = plan;
		_opts = options.Value;
		_shift = shift;
		_sink = sink;
		_plcSink = plcSink;
		_logger = logger;

		_requiredEnd = BuildRequiredEndTriggers(_plan, _stations, _opts.StationKey);

		if (_requiredEnd.Count == 0)
		{
			_logger.LogWarning(
				"Station4DefectAssignmentObserver: No end triggers found for station '{StationKey}'. " +
				"Station4 defect rows will never flush until CapturePlan.EndTriggers includes Station4 triggers.",
				_opts.StationKey);
		}
	}

	public async Task OnInspectionCompletedAsync(InspectionResult result, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		var cameraId = result.Key.CameraId;

		// Only Station4
		var stationKey = _stations.TryGetStationKey(cameraId);
		if (!string.Equals(stationKey, _opts.StationKey, StringComparison.OrdinalIgnoreCase))
			return;

		// Require camera config
		if (_opts.Cameras is null || !_opts.Cameras.TryGetValue(cameraId, out var camLayout))
			return;

		// Always track end triggers (even if no detections)
		if (_requiredEnd.Count > 0 && _requiredEnd.Contains(result.Key))
			_acc.MarkEndSeen(result.CycleId, stationKey, result.Key);

		// Aggregate counts only if we have boxes
		if (TryGetBoxes(result, _opts.InputOutputKey, out var boxes) && boxes.Count > 0)
		{
			var perFrameCounts = AssignCountsStation4(
				opts: _opts,
				cameraId: cameraId,
				triggerIndex: result.Key.Index,
				camLayout: camLayout,
				boxes: boxes);

			if (perFrameCounts.Count > 0)
				_acc.AddCounts(result.CycleId, stationKey, perFrameCounts);
		}

		// Completion check: flush when all Station4 end triggers have been seen
		if (_requiredEnd.Count > 0 &&
			_acc.TryComplete(result.CycleId, stationKey, _requiredEnd, out var finalCounts))
		{
			var (shift, shiftDate, calendarDate) = _shift.Resolve(result.CompletedAt);
			var local = TimeZoneInfo.ConvertTime(result.CompletedAt, TimeZoneInfo.Local);

			// CSV row — uses full prefixed element keys (TLB1_B1, TLB2_B1-B2, …)
			var row = new StationDefectRow(
				CycleId: result.CycleId,
				StationKey: stationKey,
				Date: shiftDate,
				CalendarDate: calendarDate,
				Timestamp: TimeOnly.FromDateTime(local.DateTime),
				Shift: shift,
				Counts: finalCounts);

			await _sink.WriteAsync(row, ct).ConfigureAwait(false);

			// PLC row — strip prefix, remap composite elements (B1-B2 → B2, B2-B3 → B2)
			if (_opts.PlcTriggerElementMap.Count > 0)
			{
				var plcRow = row with { Counts = BuildPlcCounts(finalCounts) };
				await _plcSink.WriteAsync(plcRow, ct).ConfigureAwait(false);
			}
		}
	}

	private static bool TryGetBoxes(InspectionResult result, string preferredKey, out IReadOnlyList<OverlayBox> boxes)
	{
		boxes = Array.Empty<OverlayBox>();

		var map = result.Visuals?.BoxesByStep;
		if (map is null || map.Count == 0)
			return false;

		if (!string.IsNullOrWhiteSpace(preferredKey) && map.TryGetValue(preferredKey, out boxes))
			return true;

		// fallback
		if (map.TryGetValue("YoloX_Filtered", out boxes))
			return true;
		if (map.TryGetValue("YoloX", out boxes))
			return true;

		return false;
	}

	private static Dictionary<string, int> AssignCountsStation4(
		Station4DefectAssignmentOptions opts,
		string cameraId,
		int triggerIndex,
		Station4CameraLayout camLayout,
		IReadOnlyList<OverlayBox> boxes)
	{
		var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		if (camLayout.TriggerElements is null ||
			!camLayout.TriggerElements.TryGetValue(triggerIndex, out var elem) ||
			string.IsNullOrWhiteSpace(elem))
		{
			return counts;
		}

		var prefix = !string.IsNullOrWhiteSpace(camLayout.Prefix)
			? camLayout.Prefix.Trim()
			: DerivePrefix(cameraId);

		var element = $"{prefix}_{elem.Trim()}"; // Cam1_B1-B2

		foreach (var b in boxes)
		{
			var label = b.Label;

			// Map label -> group code if configured
			if (opts.LabelToGroup is not null &&
				opts.LabelToGroup.TryGetValue(label, out var mapped) &&
				!string.IsNullOrWhiteSpace(mapped))
			{
				label = mapped;
			}

			// If you want to ONLY count known groups:
			if (opts.DefectGroups is { Length: > 0 } &&
				!opts.DefectGroups.Contains(label, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			var key = $"{label}.{element}";
			if (!counts.TryAdd(key, 1))
				counts[key]++;
		}

		return counts;
	}

	private static string DerivePrefix(string cameraId)
	{
		var n = ExtractTrailingInt(cameraId);
		return (n > 0) ? $"Cam{n}" : cameraId;
	}

	private static int ExtractTrailingInt(string s)
	{
		if (string.IsNullOrWhiteSpace(s)) return -1;

		var i = s.Length - 1;
		while (i >= 0 && char.IsDigit(s[i])) i--;

		if (i == s.Length - 1) return -1;

		var digits = s[(i + 1)..];
		return int.TryParse(digits, out var n) ? n : -1;
	}

	/// <summary>
	/// Transforms CSV-format counts (prefixed: "EPD.TLB1_B1-B2") into PLC-format counts
	/// ("EPD.B2") by stripping the camera prefix and remapping composite elements via
	/// <see cref="Station4DefectAssignmentOptions.PlcTriggerElementMap"/>.
	/// Counts for different source elements that map to the same PLC element are summed.
	/// </summary>
	private IReadOnlyDictionary<string, int> BuildPlcCounts(IReadOnlyDictionary<string, int> finalCounts)
	{
		var plcCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		foreach (var kvp in finalCounts)
		{
			// Key format: "{label}.{prefix}_{triggerElem}"  e.g. "EPD.TLB1_B1-B2"
			var parts = kvp.Key.Split('.', 2);
			if (parts.Length != 2)
				continue;

			var label = parts[0];
			var prefixedElem = parts[1];

			// Strip camera prefix ("TLB1_B1-B2" → "B1-B2")
			var underscoreIdx = prefixedElem.IndexOf('_');
			var rawElem = underscoreIdx >= 0
				? prefixedElem[(underscoreIdx + 1)..]
				: prefixedElem;

			// Map trigger element to PLC element ("B1-B2" → "B2")
			if (!_opts.PlcTriggerElementMap.TryGetValue(rawElem, out var plcElem))
				continue;

			var plcKey = $"{label}.{plcElem}";
			if (!plcCounts.TryAdd(plcKey, kvp.Value))
				plcCounts[plcKey] += kvp.Value;
		}

		return plcCounts;
	}

	private static HashSet<TriggerKey> BuildRequiredEndTriggers(
		CapturePlan plan,
		ICameraStationResolver stations,
		string stationKey)
	{
		var set = new HashSet<TriggerKey>();

		foreach (var k in plan.EndTriggers)
		{
			var st = stations.TryGetStationKey(k.CameraId);
			if (string.Equals(st, stationKey, StringComparison.OrdinalIgnoreCase))
				set.Add(k);
		}

		return set;
	}
}
