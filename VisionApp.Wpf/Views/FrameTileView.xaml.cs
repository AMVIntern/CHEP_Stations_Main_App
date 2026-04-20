using HalconDotNet;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VisionApp.Core.Domain;
using VisionApp.Wpf.ViewModels;

namespace VisionApp.Wpf.Views;

public partial class FrameTileView : UserControl
{
	private FrameTileViewModel? _vm;
	private bool _isLoaded;

	public FrameTileView()
	{
		InitializeComponent();

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
		DataContextChanged += OnDataContextChanged;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		_isLoaded = true;
		Render();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		_isLoaded = false;
		UnhookVm();
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		UnhookVm();

		_vm = DataContext as FrameTileViewModel;
		if (_vm != null)
			_vm.PropertyChanged += OnVmPropertyChanged;

		Render();
	}

	private void UnhookVm()
	{
		if (_vm != null)
		{
			_vm.PropertyChanged -= OnVmPropertyChanged;
			_vm = null;
		}
	}

	private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(FrameTileViewModel.Image) ||
			e.PropertyName == nameof(FrameTileViewModel.Visuals) ||
			e.PropertyName == nameof(FrameTileViewModel.Pass))
		{
			Render();
		}
	}

	private void Render()
	{
		if (!_isLoaded)
			return;

		var window = SmartWin?.HalconWindow;
		if (window is null)
			return;

		var image = _vm?.Image;
		var visuals = _vm?.Visuals;

		try
		{
			window.ClearWindow();

			if (image is null || !image.IsInitialized())
				return;

			HOperatorSet.GetImageSize(image, out var width, out var height);
			int w = (int)width;
			int h = (int)height;

			window.SetPart(0, 0, h - 1, w - 1);
			window.DispObj(image);

			DrawOverlays(window, visuals);
		}
		catch (HalconException)
		{
			// Don't crash UI thread
		}
	}

    private static void DrawOverlays(HWindow window, InspectionVisuals? visuals)
    {
        if (visuals is null)
            return;

        var boxes = visuals.AllBoxes;
        if (boxes.Count == 0)
            return;

        HOperatorSet.SetDraw(window, "margin");
        HOperatorSet.SetLineWidth(window, 2);      // thicker for tile
        HOperatorSet.SetColor(window, "yellow");

        double pad = 25;                           // much bigger for tile (try 15–35)

        foreach (var b in boxes)
        {
            var x = b.Rect.X;
            var y = b.Rect.Y;
            var rw = b.Rect.Width;
            var rh = b.Rect.Height;

            var row1 = Math.Max(0, y - pad);
            var col1 = Math.Max(0, x - pad);
            var row2 = y + rh + pad;
            var col2 = x + rw + pad;

            HOperatorSet.DispRectangle1(window, row1, col1, row2, col2);
        }
    }
}
