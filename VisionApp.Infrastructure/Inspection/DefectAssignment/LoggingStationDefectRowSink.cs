using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public sealed class LoggingStationDefectRowSink : IStationDefectRowSink
{
	private readonly ILogger<LoggingStationDefectRowSink> _logger;

	public LoggingStationDefectRowSink(ILogger<LoggingStationDefectRowSink> logger)
		=> _logger = logger;

	public Task WriteAsync(StationDefectRow row, CancellationToken ct)
	{
		// You’ll replace this with CSV writer later
		_logger.LogInformation("STATION ROW: Cycle={CycleId} Station={Station} Shift={Shift} Date={Date} Time={Time} Counts={CountItems}",
			row.CycleId, row.StationKey, row.Shift, row.Date, row.Timestamp,
			string.Join(", ", row.Counts.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));

		return Task.CompletedTask;
	}
}
