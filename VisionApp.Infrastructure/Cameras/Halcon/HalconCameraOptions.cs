namespace VisionApp.Infrastructure.Cameras.Halcon;

public sealed class HalconCameraOptions
{
    public const string SectionName = "HalconCameras";

    public bool Enabled { get; init; } = false;
    public int HealthIntervalMs { get; init; } = 500;

    /// <summary>Serialize open/close across cameras (recommended for HALCON stability).</summary>
    public bool SerializeOpenClose { get; init; } = true;

    public List<HalconCameraConfig> Cameras { get; init; } = new();
}

public sealed class HalconCameraConfig
{
    /// <summary>Camera ID used in TriggerKey ("Cam1", "Cam2").</summary>
    public string CameraId { get; init; } = "Cam1";

    /// <summary>HALCON device name passed into StartCameraFrameGrabber (e.g. "S4Cam1").</summary>
    public string CameraName { get; init; } = "";
}
