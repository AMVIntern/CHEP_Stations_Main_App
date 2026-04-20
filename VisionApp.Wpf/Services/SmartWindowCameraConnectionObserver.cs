using System.Windows.Threading;
using Microsoft.Extensions.Hosting;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.Services;

/// <summary>
/// Subscribes to camera connection status updates from Infrastructure
/// and pushes them into a WPF store on the UI Dispatcher.
/// </summary>
public sealed class SmartWindowCameraConnectionObserver : IHostedService
{
	private readonly ICameraConnectionStatusHub _hub;
	private readonly CameraConnectionStore _store;
	private readonly Dispatcher _dispatcher;

	public SmartWindowCameraConnectionObserver(
		ICameraConnectionStatusHub hub,
		CameraConnectionStore store,
		Dispatcher dispatcher)
	{
		_hub = hub;
		_store = store;
		_dispatcher = dispatcher;
	}

	public Task StartAsync(CancellationToken ct)
	{
		// Seed initial snapshot
		var snapshot = _hub.Snapshot.Values.ToArray();
		PushToUi(snapshot);

		// Subscribe for future changes
		_hub.StatusChanged += OnStatusChanged;
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken ct)
	{
		_hub.StatusChanged -= OnStatusChanged;
		return Task.CompletedTask;
	}

	private void OnStatusChanged(CameraConnectionStatus status)
	{
		PushToUi(status);
	}

	private void PushToUi(params CameraConnectionStatus[] statuses)
	{
		if (statuses == null || statuses.Length == 0)
			return;

		if (_dispatcher.CheckAccess())
		{
			foreach (var s in statuses)
				_store.Upsert(s);
			return;
		}

		// fire-and-forget UI marshal (event handler is sync)
		_dispatcher.BeginInvoke(() =>
		{
			foreach (var s in statuses)
				_store.Upsert(s);
		}, DispatcherPriority.Background);
	}
}
