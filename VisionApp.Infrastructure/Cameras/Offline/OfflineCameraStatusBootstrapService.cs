using Microsoft.Extensions.Hosting;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Cameras.Offline;

public sealed class OfflineCameraStatusBootstrapService : IHostedService
{
	private readonly IEnumerable<ICamera> _cameras;
	private readonly ICameraConnectionStatusHub _hub;

	public OfflineCameraStatusBootstrapService(IEnumerable<ICamera> cameras, ICameraConnectionStatusHub hub)
	{
		_cameras = cameras;
		_hub = hub;
	}

	public Task StartAsync(CancellationToken ct)
	{
		var now = DateTimeOffset.UtcNow;

		foreach (var cam in _cameras)
		{
			_hub.Upsert(new CameraConnectionStatus(
				CameraId: cam.CameraId,
				State: CameraConnectionState.Connected,
				UpdatedAt: now,
				Detail: "Offline replay"));
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
