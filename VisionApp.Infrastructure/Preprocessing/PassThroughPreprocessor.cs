using HalconDotNet;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Preprocessing;

public sealed class PassThroughPreprocessor : IFramePreprocessor
{
    public Task<FrameArrived> PreprocessAsync(RawFrameArrived raw, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Must return an OWNED image because RawFrameArrived will be disposed.
        HImage imgCopy;

        if (raw.Image is null || !raw.Image.IsInitialized())
        {
            imgCopy = new HImage(); // uninitialized but safe for disposal due to your guard
        }
        else
        {
            // Prefer CopyImage in HALCON when you want a real independent copy.
            imgCopy = raw.Image.CopyImage();
        }

        var processed = new FrameArrived(
            CycleId: raw.CycleId,
            Key: raw.Key,
            Image: imgCopy,
            CapturedAt: raw.CapturedAt);

        return Task.FromResult(processed);
    }
}
