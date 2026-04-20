using HalconDotNet;
using System.Collections.Concurrent;
using VisionApp.Core.Domain;

namespace VisionApp.Infrastructure.Inspection.Pipeline;

public sealed class InspectionContext : IDisposable
{
    public Guid CycleId { get; }
    public TriggerKey Key { get; }
    public DateTimeOffset CapturedAt { get; }

    // Source image from FrameArrived (NOT owned, do not dispose)
    public HImage SourceImage { get; }

    // Working image (owned by context) - only mutate in SEQUENTIAL phases
    public HImage WorkingImage { get; private set; }

    // Thread-safe bag for step outputs
    public ConcurrentDictionary<string, object> Items { get; } = new();

    // Everything in here will be disposed when the context is disposed.
    private readonly ConcurrentBag<IDisposable> _owned = new();

    public InspectionContext(FrameArrived frame, bool createWorkingCopy = true)
    {
        CycleId = frame.CycleId;
        Key = frame.Key;
        CapturedAt = frame.CapturedAt;
        SourceImage = frame.Image;

        if (createWorkingCopy && SourceImage?.IsInitialized() == true)
        {
            // Prefer CopyImage for a real independent copy.
            WorkingImage = SourceImage.CopyImage();
        }
        else
        {
            WorkingImage = new HImage(); // uninitialized placeholder
        }
    }

    public void Own(IDisposable d)
    {
        if (d != null) _owned.Add(d);
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (Items.TryGetValue(key, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }
        value = default!;
        return false;
    }

    public void Set<T>(string key, T value) where T : notnull
        => Items[key] = value;

    // SEQUENTIAL ONLY: replace the working image (context owns newImage afterwards)
    public void ReplaceWorkingImage(HImage newImage)
    {
        if (ReferenceEquals(WorkingImage, newImage))
            return;

        SafeDisposeImage(WorkingImage);
        WorkingImage = newImage ?? new HImage();
    }

    public void Dispose()
    {
        SafeDisposeImage(WorkingImage);

        while (_owned.TryTake(out var d))
        {
            try { d.Dispose(); } catch { /* ignore */ }
        }
    }

    private static void SafeDisposeImage(HImage? img)
    {
        try
        {
            if (img?.IsInitialized() == true)
                img.Dispose();
        }
        catch { /* ignore */ }
    }
}
