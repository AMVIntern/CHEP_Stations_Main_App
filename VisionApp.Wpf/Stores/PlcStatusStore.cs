using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisionApp.Wpf.Stores;

/// <summary>
/// UI-friendly store holding the latest PLC heartbeat health state.
/// Bind a PLC health LED directly to <see cref="IsHealthy"/>.
/// </summary>
public sealed class PlcStatusStore : INotifyPropertyChanged
{
	private bool _isHealthy;

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// True when the PLC acknowledged the last heartbeat write within the configured ack window.
	/// </summary>
	public bool IsHealthy
	{
		get => _isHealthy;
		set
		{
			if (_isHealthy == value) return;
			_isHealthy = value;
			OnPropertyChanged();
		}
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
