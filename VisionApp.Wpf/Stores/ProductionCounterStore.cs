using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisionApp.Wpf.Stores;

/// <summary>
/// Holds per-shift production metrics: shift number, total pallets, pass count (Assured), fail count (Standard).
/// Implements INotifyPropertyChanged for WPF data binding.
/// </summary>
public sealed class ProductionCounterStore : INotifyPropertyChanged
{
	private int _shiftNumber;
	private int _total;
	private int _assured;
	private int _standard;

	public int ShiftNumber
	{
		get => _shiftNumber;
		set { _shiftNumber = value; OnPropertyChanged(); }
	}

	public int Total
	{
		get => _total;
		set { _total = value; OnPropertyChanged(); }
	}

	public int Assured
	{
		get => _assured;
		set { _assured = value; OnPropertyChanged(); }
	}

	public int Standard
	{
		get => _standard;
		set { _standard = value; OnPropertyChanged(); }
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
