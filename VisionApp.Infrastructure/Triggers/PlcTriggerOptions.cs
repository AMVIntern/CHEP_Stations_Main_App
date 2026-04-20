namespace VisionApp.Infrastructure.Triggers;

public sealed class PlcTriggerOptions
{
    public const string SectionName = "PlcTriggers"; // ✅ plural

    public bool Enabled { get; init; } = true;       // ✅ add

    public string Gateway { get; init; } = "192.168.1.110";
    public string Path { get; init; } = "1,0";

    public int ReadDelayMs { get; init; } = 5;
    public int MinLowMs { get; init; } = 15;

    public List<PlcTriggerGroup> Groups { get; init; } = new();

    // Diagnostics
    public bool TraceEnabled { get; init; } = false;
    public int TraceEveryNPolls { get; init; } = 200;
    public bool TraceEveryTagRead { get; init; } = false;
    public bool TraceIgnoredEdges { get; init; } = true;

    // Optional but very useful:
    public int PollSlowWarnMs { get; init; } = 15;   // warn if tick > this
}

public sealed class PlcTriggerGroup
{
    /// <summary>
    /// One PLC BaseTag can generate triggers for multiple cameras.
    /// Example: ["Cam2","Cam3"]
    /// </summary>
    public List<string> CameraIds { get; init; } = new();
    public string BaseTag { get; init; } = "";
    public int TriggerCount { get; init; } = 5;
    public bool RequireSyncOnTrigger1 { get; init; } = true;

    /// <summary>
    /// If set, the GroupMonitor reads a DINT PalletID from this tag when trigger index 1 fires.
    /// Example: "sideStation_1_Control.PalletID"
    /// </summary>
    public string? PalletIdControlTag { get; init; }
}