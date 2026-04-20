using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

/// <summary>
/// Persists captured frames (e.g., save images to disk, attach metadata, etc.).
/// Keep this async so logging never blocks the cycle pipeline.
/// </summary>
public interface IImageLogger
{
    Task LogFrameAsync(FrameArrived frame, CancellationToken ct);
}
