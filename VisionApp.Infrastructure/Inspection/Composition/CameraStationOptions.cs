namespace VisionApp.Infrastructure.Inspection.Composition;

public sealed class CameraStationOptions
{
	public const string SectionName = "CameraStations";

	/// <summary>
	/// Map of CameraId -> StationKey (e.g. "Cam_ABC_01" -> "Station4")
	/// </summary>
	public Dictionary<string, string> Map { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
