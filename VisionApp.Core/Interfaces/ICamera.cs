using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

/// <summary>
/// Abstraction over a camera device (HALCON, OpenCV, folder replay, etc.).
/// </summary>
public interface ICamera
{
    /// <summary>
    /// Logical camera identifier used by the CapturePlan (e.g. "Cam1", "Station1_Cam2").
    /// </summary>
    string CameraId { get; }

    /// <summary>
    /// Capture an image for the given cycle + trigger key.
    /// </summary>
    Task<RawFrameArrived> CaptureAsync(CaptureRequest request, CancellationToken ct);
}

