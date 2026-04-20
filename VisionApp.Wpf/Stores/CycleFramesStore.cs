using HalconDotNet;
using System.Collections.ObjectModel;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Wpf.Services;
using VisionApp.Wpf.ViewModels;

namespace VisionApp.Wpf.Stores;

/// <summary>
/// Holds the "current cycle" set of tiles (one tile per TriggerKey).
/// - Builds tiles from CapturePlan.OrderedTriggers (so it auto-scales with recipe)
/// - On new CycleId, clears all tiles
/// - Updates the correct tile when a frame arrives
///
/// Threading: Call methods on the UI thread (we'll ensure this from the observer).
/// </summary>
public sealed class CycleFramesStore : IDisposable
{
    private readonly CapturePlan _plan;

    public ObservableCollection<FrameTileViewModel> Tiles { get; }

    public Guid CurrentCycleId { get; private set; } = Guid.Empty;

    private readonly IFrameTitleProvider _titleProvider;

    public CycleFramesStore(CapturePlan plan, IFrameTitleProvider titleProvider)
    {
        _plan = plan;
        _titleProvider = titleProvider;

        Tiles = new ObservableCollection<FrameTileViewModel>(
            _plan.OrderedTriggers.Select(k =>
            {
                var vm = new FrameTileViewModel(k);
                vm.SetTitle(_titleProvider.GetTitle(k));
                return vm;
            }));
    }


    /// <summary>
    /// Called when a new frame arrives. If the cycle changes, the store resets tiles.
    /// The provided image must be UI-owned (typically frame.Image.CopyImage()).
    /// </summary>
    public void UpdateFrame(Guid cycleId, TriggerKey key, HImage uiOwnedImage)
    {
        if (cycleId == Guid.Empty)
            throw new ArgumentException("cycleId cannot be empty.", nameof(cycleId));

        if (cycleId != CurrentCycleId)
        {
            // New product cycle → clear all tiles
            CurrentCycleId = cycleId;
            ClearAll();
        }

        var tile = Tiles.FirstOrDefault(t => t.Key.Equals(key));
        if (tile is null)
        {
            // Not in plan; dispose passed image to avoid leaks.
            uiOwnedImage.Dispose();
            return;
        }

        tile.SetImage(cycleId, uiOwnedImage);
    }

	public void UpdateResult(InspectionResult result)
	{
		if (result.CycleId == Guid.Empty)
			return;

		if (result.CycleId != CurrentCycleId)
		{
			CurrentCycleId = result.CycleId;
			ClearAll();
		}

		var tile = Tiles.FirstOrDefault(t => t.Key.Equals(result.Key));
		tile?.SetResult(result);
	}


	public void ClearAll()
    {
        foreach (var tile in Tiles)
            tile.Clear();
    }

    public void Dispose()
    {
        foreach (var tile in Tiles)
            tile.Dispose();
    }
}
