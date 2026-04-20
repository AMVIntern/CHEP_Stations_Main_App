using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface ICameraConnectionStatusHub
{
	/// <summary>
	/// Fires only when status meaningfully changes (state/detail).
	/// UI can subscribe and flip LEDs.
	/// </summary>
	event Action<CameraConnectionStatus> StatusChanged;

	/// <summary>Latest snapshot for all cameras.</summary>
	IReadOnlyDictionary<string, CameraConnectionStatus> Snapshot { get; }

	void Upsert(CameraConnectionStatus status);
}
