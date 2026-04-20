using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Sinks;

public sealed class StationDefectRowCsvSink : IStationDefectRowSink
{
	public Task WriteAsync(StationDefectRow row, CancellationToken ct)
	{
		// append to CSV here
		return Task.CompletedTask;
	}
}
