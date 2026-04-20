using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface IStationDefectRowSink
{
	Task WriteAsync(StationDefectRow row, CancellationToken ct);
}
