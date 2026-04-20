namespace VisionApp.Core.Domain;

/// <summary>
/// Unique identifier for a PLC trigger belonging to a specific camera.
/// Example: ("Cam1", 1)
/// </summary>
public readonly record struct TriggerKey(string CameraId, int Index)
{
    public override string ToString() => $"{CameraId}[{Index}]";
}