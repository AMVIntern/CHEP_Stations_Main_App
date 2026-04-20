using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Sinks;

public sealed class Station5DefectReportCsvSink : IStationDefectRowSink
{
	private readonly ILogger<Station5DefectReportCsvSink> _logger;
	private readonly Station5DefectReportCsvOptions _opts;

	private readonly SemaphoreSlim _gate = new(1, 1);

	private readonly string[] _defectGroups;
	private readonly string[] _elements;

	public Station5DefectReportCsvSink(
		IOptions<Station5DefectReportCsvOptions> opts,
		ILogger<Station5DefectReportCsvSink> logger)
	{
		_opts = opts.Value;
		_logger = logger;

		_defectGroups = NormalizeDistinctOrdered(_opts.DefectGroups);
		_elements = NormalizeDistinctOrdered(_opts.Elements);
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
				// Header row 1: Date,Timestamp,Shift,Station,RN,RN,RN...,PN...,TN...
				await sw.WriteLineAsync(BuildHeaderRow1()).ConfigureAwait(false);

				// Header row 2: ,,,,TLB1,TIB1,...,B3 repeated per group
				await sw.WriteLineAsync(BuildHeaderRow2()).ConfigureAwait(false);
			}

			// Data row: fill ALL columns; missing keys => 0
			await sw.WriteLineAsync(BuildDataRow(row)).ConfigureAwait(false);

			// Optional: pad blank lines to mimic your sample export
			for (int i = 0; i < _opts.PadBlankLinesAfterWrite; i++)
				await sw.WriteLineAsync(BuildBlankRow()).ConfigureAwait(false);

			await sw.FlushAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to write station defect report row. Station={Station} Cycle={CycleId}",
				row.StationKey, row.CycleId);
			throw;
		}
		finally
		{
			_gate.Release();
		}
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


	private static string Sanitize(string s)
	{
		foreach (var ch in Path.GetInvalidFileNameChars())
			s = s.Replace(ch, '_');
		return s.Trim();
	}
}
