using HalconDotNet;
using System.Windows.Threading;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.Services;

/// <summary>
/// WPF observer that listens to frames from the pipeline and updates the UI store.
/// Uses Dispatcher to ensure UI-thread updates.
/// Clones images (CopyImage) so UI does not depend on pipeline-owned image lifetime.
/// </summary>
public sealed class SmartWindowFrameObserver : IFrameObserver
{
    private readonly CycleFramesStore _store;
    private readonly Dispatcher _dispatcher;

    public SmartWindowFrameObserver(CycleFramesStore store, Dispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;
    }

    public async Task OnFrameArrivedAsync(FrameArrived frame, CancellationToken ct)
    {
        // Incase a null or uninitialized image is sent, ignore it.
        if (frame.Image is null || !frame.Image.IsInitialized())
            return;

        // Clone for UI ownership. UI will dispose when tile is replaced/reset.
        HImage uiCopy = frame.Image.CopyImage();

        // Marshal to UI thread
        if (_dispatcher.CheckAccess())
        {
            _store.UpdateFrame(frame.CycleId, frame.Key, uiCopy);
            return;
        }

        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                _store.UpdateFrame(frame.CycleId, frame.Key, uiCopy);
            }, DispatcherPriority.Background, ct);
        }
        catch (OperationCanceledException)
        {
            // Dispatcher operation was aborted (app shutdown) before it could run.
            // uiCopy was never handed to the store, so dispose it here to avoid leaking
            // the HALCON image.
            uiCopy.Dispose();
        }
    }
}
