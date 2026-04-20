using HalconDotNet;

namespace VisionApp.Core.Domain;

/// <summary>
/// Raw frame straight from the camera, before any preprocessing.
/// Ownership: owns the HImage and must be disposed when no longer needed.
/// </summary>
public sealed record RawFrameArrived(
    Guid CycleId,
    TriggerKey Key,
    HImage Image,
    DateTimeOffset CapturedAt) : IDisposable
{
    public void Dispose()
    {
        if (Image is null)
            return;

        if (!Image.IsInitialized())
            return;

        Image.Dispose();
    }

    public override string ToString() => $"{CapturedAt:O}  Cycle={CycleId}  RawFrame={Key}";
}
