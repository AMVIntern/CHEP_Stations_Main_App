using System.Collections.Concurrent;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Cameras.Halcon;

public sealed class CameraConnectionStatusHub : ICameraConnectionStatusHub
{
	private readonly ConcurrentDictionary<string, CameraConnectionStatus> _byId =
		new(StringComparer.OrdinalIgnoreCase);

	public event Action<CameraConnectionStatus>? StatusChanged;

	public IReadOnlyDictionary<string, CameraConnectionStatus> Snapshot => _byId;

	public void Upsert(CameraConnectionStatus status)
	{
		if (status is null || string.IsNullOrWhiteSpace(status.CameraId))
			return;

		var id = status.CameraId.Trim();

		while (true)
		{
			if (!_byId.TryGetValue(id, out var existing))
			{
				if (_byId.TryAdd(id, status))
				{
					StatusChanged?.Invoke(status);
					return;
				}
				continue;
			}

			// Only notify UI on meaningful change (state/detail).
			if (existing.State == status.State &&
				string.Equals(existing.Detail, status.Detail, StringComparison.Ordinal))
			{
				// Still update timestamp silently if you want freshness,
				// or just keep the existing (I prefer updating for diagnostics).
				_byId[id] = status;
				return;
			}

			if (_byId.TryUpdate(id, status, existing))
			{
				StatusChanged?.Invoke(status);
				return;
			}
		}
	}
}
