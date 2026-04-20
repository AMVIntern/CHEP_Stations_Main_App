using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface IFrameProcessor
{
    Task<FrameArrived> ProcessAsync(FrameArrived frame, CancellationToken ct);
}
