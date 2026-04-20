namespace VisionApp.Infrastructure.PlcOutbound;

public interface IPlcWriteQueue
{
	ValueTask EnqueueAsync(PlcWriteEntry entry, CancellationToken ct);
}
