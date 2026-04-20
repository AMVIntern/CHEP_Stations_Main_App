namespace VisionApp.Infrastructure.Cameras.Halcon;

public sealed class HalconProcedureOptions
{
    public const string SectionName = "HalconProcedures";

    /// <summary>
    /// Folder containing your .hdev procedures (StartCameraFrameGrabber, CloseFrameGrabber, etc.)
    /// </summary>
    public string ProcedurePath { get; init; } = "";

    public string StartProcName { get; init; } = "StartCameraFrameGrabber";
    public string CloseProcName { get; init; } = "CloseFrameGrabber";
}
