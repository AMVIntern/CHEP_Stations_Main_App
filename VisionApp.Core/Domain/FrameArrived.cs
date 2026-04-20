using HalconDotNet;

namespace VisionApp.Core.Domain;

/// <summary>
/// A frame/image captured by a camera for a given trigger and cycle.
/// Ownership: this object owns the HImage and must be disposed when no longer needed.
/// </summary>
public sealed record FrameArrived(
    Guid CycleId,
    TriggerKey Key,
    HImage Image,
    DateTimeOffset CapturedAt) : IDisposable
{
    public void Dispose()
    {
        // HALCON safety: disposing an uninitialized iconic object can crash the process.
        if (Image is null)
            return;

        if (!Image.IsInitialized())
            return;

        Image.Dispose();
    }


    public override string ToString() => $"{CapturedAt:O}  Cycle={CycleId}  Frame={Key}";
}
