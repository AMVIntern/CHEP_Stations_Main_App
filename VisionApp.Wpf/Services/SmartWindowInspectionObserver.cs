using System.Windows.Threading;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.Services;

public sealed class SmartWindowInspectionObserver : IInspectionObserver
{
	private readonly CycleFramesStore _store;
	private readonly Dispatcher _dispatcher;

	public SmartWindowInspectionObserver(CycleFramesStore store, Dispatcher dispatcher)
	{
		_store = store;
		_dispatcher = dispatcher;
	}

	public async Task OnInspectionCompletedAsync(InspectionResult result, CancellationToken ct)
	{
		if (_dispatcher.CheckAccess())
		{
			_store.UpdateResult(result);
			return;
		}

		await _dispatcher.InvokeAsync(() =>
		{
			_store.UpdateResult(result);
		}, DispatcherPriority.Background, ct);
	}
}
