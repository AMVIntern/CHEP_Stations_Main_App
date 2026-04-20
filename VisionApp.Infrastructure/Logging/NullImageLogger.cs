using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Logging;

/// <summary>
/// No-op image logger for v1 so the pipeline can run without any logging configured.
/// Replace with a real implementation that saves images to disk with metadata.
/// </summary>
public sealed class NullImageLogger : IImageLogger
{
    public Task LogFrameAsync(FrameArrived frame, CancellationToken ct)
        => Task.CompletedTask;
}
