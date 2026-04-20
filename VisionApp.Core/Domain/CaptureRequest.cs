namespace VisionApp.Core.Domain;

/// <summary>
/// Request produced by the CycleEngine telling a camera to capture an image
/// for a specific trigger within a specific cycle.
/// </summary>
public sealed record CaptureRequest(
    Guid CycleId,
    TriggerKey Key)
{
    public override string ToString() => $"Cycle={CycleId}  Capture={Key}";
}
