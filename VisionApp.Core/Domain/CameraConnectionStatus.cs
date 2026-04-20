namespace VisionApp.Core.Domain;

public enum CameraConnectionState
{
	Unknown = 0,
	Connected = 1,
	Disconnected = 2
}

public sealed record CameraConnectionStatus(
	string CameraId,
	CameraConnectionState State,
	DateTimeOffset UpdatedAt,
	string? Detail = null
);
