using System.Windows.Threading;
using VisionApp.Core.Interfaces;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.Services;

/// <summary>
/// Bridges PLC heartbeat health notifications from the infrastructure layer
/// into <see cref="PlcStatusStore"/> on the WPF UI dispatcher.
/// </summary>
public sealed class SmartWindowPlcStatusObserver : IPlcStatusObserver
{
	private readonly PlcStatusStore _store;
	private readonly Dispatcher _dispatcher;

	public SmartWindowPlcStatusObserver(PlcStatusStore store, Dispatcher dispatcher)
	{
		_store = store;
		_dispatcher = dispatcher;
	}

	public async Task OnPlcStatusChangedAsync(bool isHealthy, CancellationToken ct)
	{
		if (_dispatcher.CheckAccess())
		{
			_store.IsHealthy = isHealthy;
			return;
		}

		try
		{
			await _dispatcher.InvokeAsync(() =>
			{
				_store.IsHealthy = isHealthy;
			}, DispatcherPriority.Background, ct);
		}
		catch (OperationCanceledException)
		{
			// App shutting down — no need to update the store
		}
	}
}
