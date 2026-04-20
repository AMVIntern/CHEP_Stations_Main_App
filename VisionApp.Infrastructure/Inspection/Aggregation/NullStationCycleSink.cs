using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection.Aggregation;

public sealed class NullStationCycleSink : IStationCycleSink
{
	private readonly ILogger<NullStationCycleSink> _logger;

	public NullStationCycleSink(ILogger<NullStationCycleSink> logger)
	{
		_logger = logger;
	}

	public Task PublishAsync(StationCycleCompleted completed, CancellationToken ct)
	{
		_logger.LogInformation("StationCycleCompleted (NULL sink): Cycle={CycleId} Station={Station} Pass={Pass}",
			completed.CycleId, completed.StationKey, completed.OverallPass);

		return Task.CompletedTask;
	}
}
