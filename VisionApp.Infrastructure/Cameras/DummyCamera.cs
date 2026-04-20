using HalconDotNet;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Cameras;

/// <summary>
/// Dummy camera implementation that returns a placeholder image object.
/// Later replace with a HALCON-backed camera implementation.
/// </summary>
public sealed class DummyCamera : ICamera
{
    public string CameraId { get; }

    public DummyCamera(string cameraId)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
            throw new ArgumentException("CameraId cannot be null/empty.", nameof(cameraId));

        CameraId = cameraId;
    }

    public Task<RawFrameArrived> CaptureAsync(CaptureRequest request, CancellationToken ct)
    {
        // Placeholder for an image.
        // Later this will be a HALCON HImage or an OpenCV Mat wrapped in an abstraction.
        var image = new HImage();

        var frame = new RawFrameArrived(
            CycleId: request.CycleId,
            Key: request.Key,
            Image: image,
            CapturedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(frame);
    }
}
