using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VisionApp.Core.Domain;

namespace VisionApp.Wpf.Stores;

/// <summary>
/// UI-friendly store holding latest connection state per camera.
/// Bind LEDs to items in Cameras (or expose helper lookups).
/// </summary>
public sealed class CameraConnectionStore : INotifyPropertyChanged
{
	private readonly Dictionary<string, CameraConnectionItem> _byId =
		new(StringComparer.OrdinalIgnoreCase);

	public ObservableCollection<CameraConnectionItem> Cameras { get; } = new();

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool AllConnected => Cameras.Count > 0 && Cameras.All(c => c.IsConnected);

	public CameraConnectionItem? TryGet(string cameraId)
		=> _byId.TryGetValue(cameraId, out var item) ? item : null;

	public void Upsert(CameraConnectionStatus s)
	{
		if (string.IsNullOrWhiteSpace(s.CameraId))
			return;

		if (!_byId.TryGetValue(s.CameraId, out var item))
		{
			item = new CameraConnectionItem(s.CameraId);
			_byId[s.CameraId] = item;
			Cameras.Add(item);
		}

		item.State = s.State;
		item.UpdatedAt = s.UpdatedAt;
		item.Detail = s.Detail;

		// Let UI know aggregate changed
		OnPropertyChanged(nameof(AllConnected));
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// One camera row/state for binding.
/// </summary>
public sealed class CameraConnectionItem : INotifyPropertyChanged
{
	private CameraConnectionState _state = CameraConnectionState.Unknown;
	private DateTimeOffset _updatedAt;
	private string? _detail;

	public string CameraId { get; }

	public event PropertyChangedEventHandler? PropertyChanged;

	public CameraConnectionItem(string cameraId)
	{
		CameraId = cameraId;
	}

	public CameraConnectionState State
	{
		get => _state;
		set
		{
			if (_state == value) return;
			_state = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsConnected));
		}
	}

	public bool IsConnected => State == CameraConnectionState.Connected;

	public DateTimeOffset UpdatedAt
	{
		get => _updatedAt;
		set { if (_updatedAt == value) return; _updatedAt = value; OnPropertyChanged(); }
	}

	public string? Detail
	{
		get => _detail;
		set { if (_detail == value) return; _detail = value; OnPropertyChanged(); }
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
