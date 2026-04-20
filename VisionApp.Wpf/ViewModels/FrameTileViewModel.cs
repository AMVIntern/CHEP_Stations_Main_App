using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using VisionApp.Core.Domain;
using VisionApp.Wpf.Models;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels;

public sealed class FrameTileViewModel : ObservableObject, IDisposable
{
	private HImage? _image;

	private Guid _cycleId;
	private bool? _pass;
	private double? _score;
	private string? _message;
	private InspectionVisuals? _visuals;

	private readonly RelayCommand _openSnapshotCommand;

	/// <summary>
	/// Set by the composition layer (MainViewModel) to decide what happens when user clicks the tile.
	/// </summary>
	public Action<FrameTileSnapshot>? OpenSnapshotRequested { get; set; }

    private CameraConnectionItem? _connection;

    public CameraConnectionItem? Connection
    {
        get => _connection;
        private set => SetProperty(ref _connection, value);
    }

    public void SetConnection(CameraConnectionItem? connection)
    {
        Connection = connection;
    }

    public TriggerKey Key { get; }
    //public string Title => Key.ToString();
    private string _title;

    public string Title
    {
        get { return _title; }
        private set { SetProperty(ref _title, value); }
    }

    public void SetTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            Title = Key.ToString();   // fallback to existing behaviour
        else
            Title = title;
    }

    public Guid CycleId
	{
		get => _cycleId;
		private set => SetProperty(ref _cycleId, value);
	}

	public HImage? Image
	{
		get => _image;
		private set => SetProperty(ref _image, value);
	}

	public bool? Pass
	{
		get => _pass;
		private set
		{
			if (SetProperty(ref _pass, value))
				OnPropertyChanged(nameof(StatusText));
		}
	}

	public double? Score
	{
		get => _score;
		private set => SetProperty(ref _score, value);
	}

	public string? Message
	{
		get => _message;
		private set => SetProperty(ref _message, value);
	}

	public InspectionVisuals? Visuals
	{
		get => _visuals;
		private set => SetProperty(ref _visuals, value);
	}

	public string StatusText => Pass is null ? "" : (Pass.Value ? "OK" : "AOI");

	public IRelayCommand OpenSnapshotCommand => _openSnapshotCommand;

	public FrameTileViewModel(TriggerKey key)
	{
		Key = key;
        _title = key.ToString(); // default = current behaviour

        _openSnapshotCommand = new RelayCommand(
			execute: OpenSnapshot,
			canExecute: CanOpenSnapshot);
	}

	public void SetImage(Guid cycleId, HImage newImage)
	{
		CycleId = cycleId;

		_image?.Dispose();
		Image = newImage;

		_openSnapshotCommand.NotifyCanExecuteChanged();
	}

	public void SetResult(InspectionResult result)
	{
		if (!result.Key.Equals(Key))
			return;

		CycleId = result.CycleId;

		Pass = result.Pass;
		Score = result.Score;
		Message = result.Message;
		Visuals = result.Visuals;
	}

	private bool CanOpenSnapshot()
		=> Image is not null && Image.IsInitialized();

	private void OpenSnapshot()
	{
		var snap = TryCreateSnapshot();
		if (snap is null)
			return;

		var handler = OpenSnapshotRequested;
		if (handler is null)
		{
			// No handler registered -> avoid leaking the cloned image
			snap.Dispose();
			return;
		}

		handler(snap);
	}

	public FrameTileSnapshot? TryCreateSnapshot()
	{
		if (Image is null || !Image.IsInitialized())
			return null;

		// Clone so snapshot lifetime is independent of tile updates
		var imgCopy = Image.CopyImage();

		// Deep-ish copy of visuals (they're immutable records but lists/dicts are safer to clone)
		InspectionVisuals? visualsCopy = null;
		if (Visuals is not null)
		{
			var dict = new Dictionary<string, IReadOnlyList<OverlayBox>>(StringComparer.OrdinalIgnoreCase);
			foreach (var kvp in Visuals.BoxesByStep)
				dict[kvp.Key] = kvp.Value.ToList(); // OverlayBox is immutable

			visualsCopy = new InspectionVisuals(dict);
		}

		return new FrameTileSnapshot(
			cycleId: CycleId,
			key: Key,
			image: imgCopy,
			pass: Pass,
			score: Score,
			message: Message,
			visuals: visualsCopy);
	}

	public void Clear()
	{
		_image?.Dispose();
		Image = null;

		CycleId = Guid.Empty;
		Pass = null;
		Score = null;
		Message = null;
		Visuals = null;

		_openSnapshotCommand.NotifyCanExecuteChanged();
	}

	public void Dispose() => Clear();
}
