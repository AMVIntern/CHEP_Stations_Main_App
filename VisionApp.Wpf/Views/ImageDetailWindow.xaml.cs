using HalconDotNet;
using System.ComponentModel;
using System.Windows;
using VisionApp.Core.Domain;
using VisionApp.Wpf.ViewModels;

namespace VisionApp.Wpf.Views;

public partial class ImageDetailWindow : Window
{
	private ImageDetailViewModel? _vm;
	private bool _isLoaded;

	public ImageDetailWindow()
	{
		InitializeComponent();

		Loaded += (_, __) =>
		{
			_isLoaded = true;
			Render();
		};

		Unloaded += (_, __) => _isLoaded = false;

		DataContextChanged += OnDataContextChanged;
		Closed += OnClosed;
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (_vm != null)
			_vm.PropertyChanged -= OnVmPropertyChanged;

		_vm = DataContext as ImageDetailViewModel;

		if (_vm != null)
			_vm.PropertyChanged += OnVmPropertyChanged;

		Render();
	}

	private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ImageDetailViewModel.Image) ||
			e.PropertyName == nameof(ImageDetailViewModel.Visuals))
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
			// keep UI alive
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
        HOperatorSet.SetLineWidth(window, 1);
        HOperatorSet.SetColor(window, "yellow");

        double pad = 8; // <-- increase this to make the box bigger (try 4, 6, 8, 10)

        foreach (var b in boxes)
        {
            var x = b.Rect.X;
            var y = b.Rect.Y;
            var rw = b.Rect.Width;
            var rh = b.Rect.Height;

            // Inflate rectangle
            var row1 = Math.Max(0, y - pad);
            var col1 = Math.Max(0, x - pad);
            var row2 = y + rh + pad;
            var col2 = x + rw + pad;

            HOperatorSet.DispRectangle1(window, row1, col1, row2, col2);
        }
    }


    private void OnBackClick(object sender, RoutedEventArgs e) => Close();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
	{
		// Ensure snapshot image is disposed when window closes
		if (DataContext is IDisposable d)
			d.Dispose();
	}
}
