using System.Windows;
using System.Windows.Threading;
using VisionApp.Wpf.Models;
using VisionApp.Wpf.ViewModels;
using VisionApp.Wpf.Views;

namespace VisionApp.Wpf.Services;

public sealed class ImageViewerService
{
	private readonly Dispatcher _dispatcher;
	private readonly object _gate = new();

	private ImageDetailWindow? _window;

	public ImageViewerService(Dispatcher dispatcher)
	{
		_dispatcher = dispatcher;
	}

	public void OpenSnapshot(FrameTileSnapshot snapshot)
	{
		if (_dispatcher.CheckAccess())
		{
			OpenInternal(snapshot);
			return;
		}

		_dispatcher.Invoke(() => OpenInternal(snapshot));
	}

	private void OpenInternal(FrameTileSnapshot snapshot)
	{
		lock (_gate)
		{
			// If the window was closed, create a new one.
			if (_window == null)
			{
				_window = new ImageDetailWindow
				{
					Owner = Application.Current?.MainWindow
				};

				// When user closes the window, drop our reference (so next click recreates it)
				_window.Closed += (_, __) =>
				{
					lock (_gate)
					{
						_window = null;
					}
				};
			}
			else
			{
				// Reuse existing window: dispose old VM (and its snapshot image) to avoid leaks
				if (_window.DataContext is IDisposable oldVm)
				{
					try { oldVm.Dispose(); } catch { /* ignore */ }
				}
			}

			// Set new snapshot VM
			_window.DataContext = new ImageDetailViewModel(snapshot);

			// Show/restore/focus
			if (!_window.IsVisible)
				_window.Show();

			if (_window.WindowState == WindowState.Minimized)
				_window.WindowState = WindowState.Normal;

			_window.Activate();
			_window.Focus();
		}
	}
}
