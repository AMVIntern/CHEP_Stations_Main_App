using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Cameras.Halcon;

public sealed class HalconCameraHealthService : BackgroundService
{
	private readonly ILogger<HalconCameraHealthService> _logger;
	private readonly HalconCameraOptions _opts;
	private readonly IEnumerable<ICamera> _cameras;
	private readonly ICameraConnectionStatusHub _hub;

	// Ensure we don't start infinite reconnect loops multiple times per camera
	private readonly ConcurrentDictionary<string, Task> _reconnectTasks =
		new(StringComparer.OrdinalIgnoreCase);

	public HalconCameraHealthService(
		ILogger<HalconCameraHealthService> logger,
		HalconCameraOptions opts,
		IEnumerable<ICamera> cameras,
		ICameraConnectionStatusHub hub)
	{
		_logger = logger;
		_opts = opts;
		_cameras = cameras;
		_hub = hub;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_opts.Enabled)
		{
			_logger.LogInformation("HalconCameraHealthService disabled by config.");
			return;
		}

		// Seed initial statuses as Unknown so UI can show "grey" until first tick
		var now = DateTimeOffset.UtcNow;
		foreach (var cam in _cameras.OfType<HalconGigECamera>())
			_hub.Upsert(new CameraConnectionStatus(cam.CameraId, CameraConnectionState.Unknown, now));

		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(50, _opts.HealthIntervalMs)));

		_logger.LogInformation("HalconCameraHealthService started. IntervalMs={Interval}",
			_opts.HealthIntervalMs);

		while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
		{
			await TickAsync(stoppingToken).ConfigureAwait(false);
		}
	}

	private Task TickAsync(CancellationToken ct)
	{
		var now = DateTimeOffset.UtcNow;

		foreach (var cam in _cameras.OfType<HalconGigECamera>())
		{
			var id = cam.CameraId;

			bool alive;
			try
			{
				alive = cam.IsAlive();
			}
			catch (Exception ex)
			{
				alive = false;
				_hub.Upsert(new CameraConnectionStatus(id, CameraConnectionState.Disconnected, now, ex.Message));
			}

			if (alive)
			{
				_hub.Upsert(new CameraConnectionStatus(id, CameraConnectionState.Connected, now));
				_reconnectTasks.TryRemove(id, out _);
				continue;
			}

			// Mark disconnected immediately for UI
			_hub.Upsert(new CameraConnectionStatus(id, CameraConnectionState.Disconnected, now, "Not alive"));

			// Start (or keep) a reconnect task without blocking the whole health loop
			_reconnectTasks.AddOrUpdate(
				id,
				_ => StartReconnect(cam, ct),
				(_, existing) => existing.IsCompleted ? StartReconnect(cam, ct) : existing);
		}

		return Task.CompletedTask;
	}

	private Task StartReconnect(HalconGigECamera cam, CancellationToken ct)
	{
		var id = cam.CameraId;

		return Task.Run(async () =>
		{
			try
			{
				_logger.LogWarning("Camera {CameraId} disconnected; starting reconnect loop.", id);
				_hub.Upsert(new CameraConnectionStatus(id, CameraConnectionState.Disconnected, DateTimeOffset.UtcNow, "Reconnecting"));

				await cam.EnsureConnectedAsync(ct).ConfigureAwait(false);

				_hub.Upsert(new CameraConnectionStatus(id, CameraConnectionState.Connected, DateTimeOffset.UtcNow, "Reconnected"));
				_logger.LogInformation("Camera {CameraId} reconnected.", id);
			}
			catch (OperationCanceledException)
			{
				// normal shutdown
			}
			catch (Exception ex)
			{
				_hub.Upsert(new CameraConnectionStatus(id, CameraConnectionState.Disconnected, DateTimeOffset.UtcNow, ex.Message));
				_logger.LogError(ex, "Reconnect task failed for camera {CameraId}", id);
			}
		}, ct);
	}
}
