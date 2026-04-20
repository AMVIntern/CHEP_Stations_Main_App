using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.PlcOutbound;

public sealed class PlcStationDefectBoolSink : IStationDefectRowSink
{
	private readonly PlcOutboundOptions _opts;
	private readonly IPlcWriteQueue _queue;
	private readonly IPalletIdStore _palletIdStore;
	private readonly ILogger<PlcStationDefectBoolSink> _logger;

	private readonly Dictionary<string, PlcStationResultsOptions> _stationCfg;

	public PlcStationDefectBoolSink(
		IOptions<PlcOutboundOptions> opts,
		IPlcWriteQueue queue,
		IPalletIdStore palletIdStore,
		ILogger<PlcStationDefectBoolSink> logger)
	{
		_opts = opts.Value;
		_queue = queue;
		_palletIdStore = palletIdStore;
		_logger = logger;

		_stationCfg = _opts.Results.Stations
			.Where(s => !string.IsNullOrWhiteSpace(s.StationKey))
			.ToDictionary(s => s.StationKey, s => s, StringComparer.OrdinalIgnoreCase);
	}

	public async Task WriteAsync(StationDefectRow row, CancellationToken ct)
	{
		if (!_opts.Enabled)
			return;

		if (!_stationCfg.TryGetValue(row.StationKey, out var cfg))
		{
			_logger.LogDebug("PLC sink: no config found for station '{Station}' — skipping write.", row.StationKey);
			return;
		}

		// Build a lookup of (Element, PlcDefect) -> present
		var present = new HashSet<(string Element, string PlcDefect)>();

		foreach (var kvp in row.Counts)
		{
			// expects "RN.BIB3" or "EPD.B1"
			var parts = kvp.Key.Split('.', 2);
			if (parts.Length != 2)
				continue;

			var label = parts[0];
			var element = parts[1];

			// any count > 0 means "present"
			if (kvp.Value <= 0)
				continue;

			if (!_opts.Results.LabelToPlcDefect.TryGetValue(label, out var plcDefect))
			{
				_logger.LogDebug("PLC sink: label '{Label}' has no LabelToPlcDefect mapping — skipped.", label);
				continue;
			}

			present.Add((element, plcDefect));
		}

		int totalTags = cfg.Elements.Count * cfg.Defects.Count;
		int trueTags = 0;

		// Write ALL configured bool tags every cycle (present => true, missing => false).
		// "B_" elements (B1, B2, B3 – bearer-level tags) omit the ".Defects." segment
		// because the PLC tag path is structured differently for those elements.
		foreach (var element in cfg.Elements)
		{
			foreach (var defect in cfg.Defects)
			{
				var tagName = IsSimpleBearerElement(element)
					? $"{cfg.BaseTag}.{element}.{defect}"
					: $"{cfg.BaseTag}.{element}.Defects.{defect}";

				var value = present.Contains((element, defect));
				if (value) trueTags++;

				_logger.LogDebug("PLC enqueue (BOOL): {Tag} = {Value}", tagName, value);

				await _queue.EnqueueAsync(new PlcBoolWrite(tagName, value), ct);
			}
		}

		// Write PalletID DINT after all bool tags — signals write-cycle complete to PLC.
		if (!string.IsNullOrWhiteSpace(cfg.TimestampTagSuffix))
		{
			var tsTag = $"{cfg.BaseTag}.{cfg.TimestampTagSuffix}";
			var palletId = _palletIdStore.Get(row.StationKey);

			_logger.LogDebug("PLC enqueue (DINT palletId): {Tag} = {Value}", tsTag, palletId);

			await _queue.EnqueueAsync(new PlcDintWrite(tsTag, palletId), ct);
		}

		_logger.LogInformation(
			"PLC write dispatched — Station={Station} CycleId={CycleId}: {TrueCount}/{TotalCount} tags SET (true). Detected: [{Detected}]",
			row.StationKey,
			row.CycleId,
			trueTags,
			totalTags,
			string.Join(", ", present.Select(p => $"{p.Element}.{p.PlcDefect}")));
	}

	/// <summary>
	/// Returns true for "B_" elements whose tag path omits the ".Defects." segment —
	/// i.e. the element is exactly the letter B followed by one or more digits (B1, B2, B3).
	/// All other elements (BLB1, BIB2, …) still include ".Defects." in the path.
	/// </summary>
	private static bool IsSimpleBearerElement(string element)
	{
		if (element.Length < 2 || element[0] != 'B')
			return false;

		for (int i = 1; i < element.Length; i++)
			if (!char.IsDigit(element[i]))
				return false;

		return true;
	}
}
