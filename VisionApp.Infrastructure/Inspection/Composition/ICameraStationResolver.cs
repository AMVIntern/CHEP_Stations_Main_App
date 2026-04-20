namespace VisionApp.Infrastructure.Inspection.Composition;

public interface ICameraStationResolver
{
	/// <summary>Returns StationKey for cameraId, or null if not configured.</summary>
	string? TryGetStationKey(string cameraId);
}
