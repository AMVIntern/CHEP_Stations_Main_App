namespace VisionApp.Infrastructure.Logging;

/// <summary>
/// Disk image logging configuration.
/// Supports grouping cameras into named folders.
/// </summary>
public sealed class ImageLoggingOptions
{
    public const string SectionName = "ImageLogging";

    public bool Enabled { get; init; } = true;

    /// <summary>Root folder for all logs.</summary>
    public string RootFolder { get; init; } = @"C:\VisionAppLogs\Images";

    /// <summary>Image format: png, jpg, tiff, bmp</summary>
    public string Format { get; init; } = "png";

    public int JpegQuality { get; init; } = 90;

    /// <summary>
    /// If true, cycle folders include CycleId to guarantee uniqueness.
    /// Example: 20260120_145330_123__a1b2c3...
    /// </summary>
    public bool IncludeCycleIdInFolder { get; init; } = true;

    /// <summary>
    /// If true, create a CameraId subfolder inside each cycle folder.
    /// Recommended when groups contain multiple cameras.
    /// </summary>
    public bool IncludeCameraSubfolder { get; init; } = true;

    /// <summary>Capacity of the log queue. Prevents unbounded RAM usage.</summary>
    public int QueueCapacity { get; init; } = 500;

    /// <summary>
    /// If true, logger will drop frames when queue is full (never blocks inspection).
    /// If false, logger will backpressure and wait.
    /// </summary>
    public bool DropWhenBusy { get; init; } = true;

    /// <summary>Fallback folder name if camera is not mapped.</summary>
    public string UnknownGroupName { get; init; } = "_Unknown";

    /// <summary>
    /// Camera grouping configuration.
    /// Example: GroupName="TopView", CameraIds=["Cam1","Cam2"]
    /// </summary>
    public List<CameraGroup> Groups { get; init; } = new();

    /// <summary>
    /// If true, create a Month folder (MM) under GroupName.
    /// </summary>
    public bool UseMonthSubfolder { get; init; } = true;

    /// <summary>
    /// If true, create a Day folder (dd) under the Month folder.
    /// </summary>
    public bool UseDaySubfolder { get; init; } = true;

}

/// <summary>
/// Defines a named folder group containing one or more cameras.
/// </summary>
public sealed class CameraGroup
{
    public string GroupName { get; init; } = string.Empty;
    public List<string> CameraIds { get; init; } = new();
}
