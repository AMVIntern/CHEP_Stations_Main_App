using HalconDotNet;
using OpenCvSharp;

namespace VisionApp.Infrastructure.Inspection.Pipeline;

public sealed class InspectionArtifacts : IDisposable
{
    private readonly List<IDisposable> _owned = new();
    private readonly object _gate = new();

    public List<Rect> Rects { get; } = new();
    public List<(string Label, Rect Rect)> LabeledRects { get; } = new();

    // If you really need HRegion overlays:
    public List<HRegion> FailRegions { get; } = new();

    // If you really need Mat crops:
    public List<Mat> CropMats { get; } = new();

    public void Own(IDisposable d)
    {
        lock (_gate) _owned.Add(d);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var d in _owned)
            {
                try { d.Dispose(); } catch { }
            }
            _owned.Clear();
        }
    }
}

