using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.PlcOutbound;

namespace VisionApp.Infrastructure.Sinks;

public sealed class FanoutStationDefectRowSink : IStationDefectRowSink
{
	private readonly Station5DefectReportCsvSink _csv;
	private readonly PlcStationDefectBoolSink _plc;

	public FanoutStationDefectRowSink(
		Station5DefectReportCsvSink csv,
		PlcStationDefectBoolSink plc)
	{
		_csv = csv;
		_plc = plc;
	}

	public async Task WriteAsync(StationDefectRow row, CancellationToken ct)
	{
		// Run both. You can change ordering if you want.
		await _plc.WriteAsync(row, ct).ConfigureAwait(false);
		await _csv.WriteAsync(row, ct).ConfigureAwait(false);
	}
}
