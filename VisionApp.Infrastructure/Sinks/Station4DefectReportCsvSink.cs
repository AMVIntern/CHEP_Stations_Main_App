using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using VisionApp.Core.Domain;
using VisionApp.Infrastructure.Inspection.DefectAssignment;

namespace VisionApp.Infrastructure.Sinks;

/// <summary>
/// Station4-specific CSV report:
/// - Column layout is derived from Station4DefectAssignmentOptions:
///   DefectGroups x (Cam1_B1, Cam1_B1-B2, Cam1_B2-B3, Cam1_B3, Cam2_..., ...)
/// - Writes to a separate folder / file set from Station5.
/// </summary>
public sealed class Station4DefectReportCsvSink
{
	private readonly ILogger<Station4DefectReportCsvSink> _logger;
	private readonly Station4DefectReportCsvOptions _opts;

	private readonly string[] _defectGroups;
	private readonly string[] _elements;

	private readonly SemaphoreSlim _gate = new(1, 1);

	public Station4DefectReportCsvSink(
		IOptions<Station4DefectReportCsvOptions> csvOpts,
		IOptions<Station4DefectAssignmentOptions> assignOpts,
		ILogger<Station4DefectReportCsvSink> logger)
	{
		_opts = csvOpts.Value;
		_logger = logger;

		var a = assignOpts.Value;

		_defectGroups = NormalizeDistinctOrdered(
			(a.DefectGroups?.Length > 0)
				? a.DefectGroups
				: a.LabelToGroup.Values.ToArray());

		_elements = NormalizeDistinctOrdered(BuildElements(a));
	}

	public async Task WriteAsync(StationDefectRow row, CancellationToken ct)
	{
		if (!_opts.Enabled)
			return;

		ct.ThrowIfCancellationRequested();

		await _gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			var folder = _opts.UseStationSubfolder
				? Path.Combine(_opts.RootFolder, Sanitize(row.StationKey))
				: _opts.RootFolder;

			Directory.CreateDirectory(folder);

			var fileDate = row.Date.ToString(_opts.FileDateFormat, CultureInfo.InvariantCulture);
			var fileName = _opts.FileNamePattern
				.Replace("{Station}", Sanitize(row.StationKey))
				.Replace("{Shift}", row.Shift.ToString(CultureInfo.InvariantCulture))
				.Replace("{FileDate}", fileDate);

			var path = Path.Combine(folder, fileName);

			var isNew = !File.Exists(path) || new FileInfo(path).Length == 0;

			await using var fs = new FileStream(
				path,
				FileMode.Append,
				FileAccess.Write,
				FileShare.Read);

			await using var sw = new StreamWriter(fs, new UTF8Encoding(false));

			if (isNew)
			{
				await sw.WriteLineAsync(BuildHeaderRow1()).ConfigureAwait(false);
				await sw.WriteLineAsync(BuildHeaderRow2()).ConfigureAwait(false);
			}

			await sw.WriteLineAsync(BuildDataRow(row)).ConfigureAwait(false);

			for (int i = 0; i < _opts.PadBlankLinesAfterWrite; i++)
				await sw.WriteLineAsync(BuildBlankRow()).ConfigureAwait(false);

			await sw.FlushAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to write Station4 defect report row. Station={Station} Cycle={CycleId}",
				row.StationKey, row.CycleId);
			throw;
		}
		finally
		{
			_gate.Release();
		}
	}

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

	private string BuildHeaderRow1()
	{
		var cols = new List<string> { "Date", "Timestamp", "Shift", "Station" };

		foreach (var g in _defectGroups)
			for (int i = 0; i < _elements.Length; i++)
				cols.Add(g);

		return string.Join(",", cols);
	}

	private string BuildHeaderRow2()
	{
		var cols = new List<string> { "", "", "", "" };

		foreach (var _ in _defectGroups)
			cols.AddRange(_elements);

		return string.Join(",", cols);
	}

	private string BuildDataRow(StationDefectRow row)
	{
		var date = row.CalendarDate.ToString(_opts.RowDateFormat, CultureInfo.InvariantCulture);
		var time = row.Timestamp.ToString(_opts.RowTimeFormat, CultureInfo.InvariantCulture);

		var cols = new List<string>
		{
			date,
			time,
			row.Shift.ToString(CultureInfo.InvariantCulture),
			row.StationKey
		};

		foreach (var g in _defectGroups)
		{
			foreach (var el in _elements)
			{
				var key = $"{g}.{el}";
				var v = row.Counts.TryGetValue(key, out var val) ? val : 0;
				cols.Add(v.ToString(CultureInfo.InvariantCulture));
			}
		}

		return string.Join(",", cols);
	}

	private string BuildBlankRow()
	{
		var totalCols = 4 + (_defectGroups.Length * _elements.Length);
		return new string(',', totalCols - 1);
	}

	private static string[] BuildElements(Station4DefectAssignmentOptions opts)
	{
		if (opts.Cameras is null || opts.Cameras.Count == 0)
			return Array.Empty<string>();

		// Deterministic camera order: Cam1, Cam2... if possible
		var cams = opts.Cameras
			.OrderBy(kvp => ExtractTrailingInt(kvp.Key))
			.ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

		var list = new List<string>();

		foreach (var (cameraId, layout) in cams)
		{
			var prefix = !string.IsNullOrWhiteSpace(layout.Prefix)
				? layout.Prefix.Trim()
				: DerivePrefix(cameraId);

			if (layout.TriggerElements is null || layout.TriggerElements.Count == 0)
				continue;

			foreach (var idx in layout.TriggerElements.Keys.OrderBy(x => x))
			{
				var element = layout.TriggerElements[idx];
				if (string.IsNullOrWhiteSpace(element))
					continue;

				list.Add($"{prefix}_{element.Trim()}");
			}
		}

		return list.ToArray();
	}

	private static string DerivePrefix(string cameraId)
	{
		var n = ExtractTrailingInt(cameraId);
		return (n > 0) ? $"Cam{n}" : cameraId;
	}

	private static int ExtractTrailingInt(string s)
	{
		if (string.IsNullOrWhiteSpace(s)) return int.MaxValue;

		// Pull trailing digits (S4Cam1 -> 1)
		var i = s.Length - 1;
		while (i >= 0 && char.IsDigit(s[i])) i--;

		if (i == s.Length - 1) return int.MaxValue;

		var digits = s[(i + 1)..];
		return int.TryParse(digits, out var n) ? n : int.MaxValue;
	}

	private static string Sanitize(string s)
	{
		foreach (var c in Path.GetInvalidFileNameChars())
			s = s.Replace(c, '_');
		return s.Trim();
	}
}
