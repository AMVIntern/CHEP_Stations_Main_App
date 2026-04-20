namespace VisionApp.Infrastructure.Cameras;

/// <summary>
/// Configuration for folder replay cameras.
/// </summary>
public sealed class FolderReplayOptions
{
    public const string SectionName = "FolderReplay";

    public string Cam1Folder { get; init; } = string.Empty;
    public string Cam2Folder { get; init; } = string.Empty;
    public string S5Cam1Folder { get; init; } = string.Empty;
    public string S5Cam2Folder { get; init; } = string.Empty;
    public string S5Cam3Folder { get; init; } = string.Empty;
	public string S5Cam4Folder { get; init; } = string.Empty;

	/// <summary>
	/// If true, wrap around to the first image after reaching the end.
	/// </summary>
	public bool Loop { get; init; } = true;

    /// <summary>
    /// If true, restrict to common image extensions (recommended).
    /// </summary>
    public bool FilterExtensions { get; init; } = true;
}
