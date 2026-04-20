using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using VisionApp.Core.Domain;
using VisionApp.Wpf.Models;

namespace VisionApp.Wpf.ViewModels;

public sealed class ImageDetailViewModel : ObservableObject, IDisposable
{
	public FrameTileSnapshot Snapshot { get; }

	public string Title => $"{Snapshot.Key}  Cycle={Snapshot.CycleId}";
	public HImage Image => Snapshot.Image;
	public InspectionVisuals? Visuals => Snapshot.Visuals;

	public bool? Pass => Snapshot.Pass;
	public string StatusText => Pass is null ? "" : (Pass.Value ? "OK" : "FAIL");
	public string? Message => Snapshot.Message;

	public ImageDetailViewModel(FrameTileSnapshot snapshot)
	{
		Snapshot = snapshot;
	}

	public void Dispose() => Snapshot.Dispose();
}
