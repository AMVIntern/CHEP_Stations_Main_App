using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface IStationCycleSink
{
	Task PublishAsync(StationCycleCompleted completed, CancellationToken ct);
}
