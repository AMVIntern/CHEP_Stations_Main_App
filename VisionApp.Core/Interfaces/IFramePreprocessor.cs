using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface IFramePreprocessor
{
    Task<FrameArrived> PreprocessAsync(RawFrameArrived raw, CancellationToken ct);
}
