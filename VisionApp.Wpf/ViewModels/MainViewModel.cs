using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using VisionApp.Wpf.Models;
using VisionApp.Wpf.Services;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels;

/// <summary>
/// Record for station header info: display name and proportional column width
/// </summary>
public sealed record StationHeaderInfo(string DisplayName, GridLength StarWidth);

/// <summary>
/// Simple birds-eye "Home" VM:
/// - Cameras side-by-side (horizontal)
/// - Tiles within each camera top-to-bottom (vertical)
/// - Station headers above camera groups
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
	public ObservableCollection<CameraGroupViewModel> CameraGroups { get; }
	public IReadOnlyList<StationHeaderInfo> StationHeaders { get; }

    public int CameraGroupColumns
    {
        get
        {
            int count = CameraGroups != null ? CameraGroups.Count : 0;
            return count <= 0 ? 1 : count;
        }
    }

    public Orientation CameraGroupsOrientation => Orientation.Horizontal;
	public Orientation TilesOrientation => Orientation.Vertical;

    public MainViewModel(
        CycleFramesStore store,
        ImageViewerService viewer,
        CameraConnectionStore cameraConnectionStore,
        IOptions<UiFrameTitlesSettings> frameTitlesOptions)
    {
        // Hook tile click -> open snapshot window
        foreach (var tile in store.Tiles)
        {
            tile.OpenSnapshotRequested = snap => viewer.OpenSnapshot(snap);

            // Bind each tile to its camera connection item
            var camId = tile.Key.CameraId;
            tile.SetConnection(cameraConnectionStore.TryGet(camId));
        }

        // If camera items appear later, bind them then
        cameraConnectionStore.Cameras.CollectionChanged += (_, __) =>
        {
            foreach (var tile in store.Tiles)
            {
                if (tile.Connection == null)
                {
                    tile.SetConnection(cameraConnectionStore.TryGet(tile.Key.CameraId));
                }
            }
        };

        var groups = store.Tiles
            .GroupBy(t => t.Key.CameraId)
            .OrderBy(g => g.Key, System.StringComparer.OrdinalIgnoreCase)
            .Select(g => new CameraGroupViewModel(
                cameraId: g.Key,
                tiles: g.OrderBy(t => t.Key.Index).ToList()))
            .ToList();

        CameraGroups = new ObservableCollection<CameraGroupViewModel>(groups);

        // Build station headers from UI frame titles config
        StationHeaders = frameTitlesOptions.Value.Stations
            .Select(s => new StationHeaderInfo(
                DisplayName: string.IsNullOrWhiteSpace(s.DisplayName) ? s.StationKey : s.DisplayName,
                StarWidth: new GridLength(s.CameraElements.Count, GridUnitType.Star)))
            .ToList();
    }
}

public sealed class CameraGroupViewModel
{
    public string CameraId { get; }
    public string Title => CameraId;

    public ObservableCollection<FrameTileViewModel> Tiles { get; }
    public int TilesRows => Tiles.Count <= 0 ? 1 : Tiles.Count;

    public CameraGroupViewModel(string cameraId, IReadOnlyList<FrameTileViewModel> tiles)
    {
        CameraId = cameraId;
        Tiles = new ObservableCollection<FrameTileViewModel>(tiles);
    }
}
