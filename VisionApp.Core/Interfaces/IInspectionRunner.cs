using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

/// <summary>
/// Runs an inspection on a captured frame (HALCON / ONNX / hybrid).
/// Implementations should be stateless or internally thread-safe.
/// </summary>
public interface IInspectionRunner
{
    Task<InspectionResult> InspectAsync(FrameArrived frame, CancellationToken ct);
}
