using HalconDotNet;
using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Preprocessing;

public sealed class GammaPreprocessor : IFramePreprocessor
{
    private readonly ILogger<GammaPreprocessor> _logger;

    private const bool Enabled = true;

    private static readonly HashSet<string> SkipCameras = new(StringComparer.OrdinalIgnoreCase)
    {
        // "Cam2"
    };

    private const double Gamma = 0.35;
    private const double Offset = 0.0;
    private const double Threshold = 0.0;
    private const double MaxGray = 200.0;

    public GammaPreprocessor(ILogger<GammaPreprocessor> logger)
    {
        _logger = logger;
    }

    public Task<FrameArrived> PreprocessAsync(RawFrameArrived raw, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // If raw image is missing/uninitialized => return empty frame
        if (raw.Image is null || !raw.Image.IsInitialized())
        {
            return Task.FromResult(new FrameArrived(
                CycleId: raw.CycleId,
                Key: raw.Key,
                Image: new HImage(),          // uninitialized placeholder
                CapturedAt: raw.CapturedAt));
        }

        // If disabled or camera skipped => just pass-through clone (NO gamma)
        if (!Enabled || SkipCameras.Contains(raw.Key.CameraId))
        {
            return Task.FromResult(new FrameArrived(
                CycleId: raw.CycleId,
                Key: raw.Key,
                Image: raw.Image.Clone(),     // clone so FrameArrived owns it
                CapturedAt: raw.CapturedAt));
        }

        // Otherwise apply gamma once
        try
        {
            HObject obj;
            HOperatorSet.GammaImage(raw.Image, out obj, Gamma, Offset, Threshold, MaxGray, "true");

            // HImage acquires its own HALCON reference when constructed from HObject.
            // Dispose obj immediately so we don't leave a dangling reference waiting
            // for the finalizer — at high frame rates this adds measurable GC pressure.
            var gammaImg = new HImage(obj);
            obj.Dispose();

            // gammaImg is the new independently-owned image; no clone needed.
            return Task.FromResult(new FrameArrived(
                CycleId: raw.CycleId,
                Key: raw.Key,
                Image: gammaImg,
                CapturedAt: raw.CapturedAt));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gamma preprocessing failed for {Key}. Using raw image.", raw.Key);

            // Fallback: return raw clone so pipeline continues
            return Task.FromResult(new FrameArrived(
                CycleId: raw.CycleId,
                Key: raw.Key,
                Image: raw.Image.Clone(),
                CapturedAt: raw.CapturedAt));
        }
    }
}
