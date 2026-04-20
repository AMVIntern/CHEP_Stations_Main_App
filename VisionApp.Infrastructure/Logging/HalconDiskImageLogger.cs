using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Logging;

/// <summary>
/// Enqueues frames for disk logging.
/// - Clones the HImage (CopyImage) so the pipeline can safely dispose its original.
/// - Pushes into ImageLogQueue (background writer will save & dispose).
/// </summary>
public sealed class HalconDiskImageLogger : IImageLogger
{
    private readonly ImageLoggingOptions _options;
    private readonly ImageLogQueue _queue;
    private readonly ILogger<HalconDiskImageLogger> _logger;

    private readonly Dictionary<string, string> _cameraToGroup;

    public HalconDiskImageLogger(
        ImageLoggingOptions options,
        ImageLogQueue queue,
        ILogger<HalconDiskImageLogger> logger)
    {
        _options = options;
        _queue = queue;
        _logger = logger;

        _cameraToGroup = BuildCameraGroupMap(options);
    }

    public async Task LogFrameAsync(FrameArrived frame, CancellationToken ct)
    {
        if (!_options.Enabled)
            return;

        if (frame.Image is null || !frame.Image.IsInitialized())
            return;

        var groupName = ResolveGroupName(frame.Key.CameraId);

        ImageLogItem? item = null;

        try
        {
            // Logger must own its own copy.
            var clone = frame.Image.CopyImage();

            item = new ImageLogItem(
                CycleId: frame.CycleId,
                Key: frame.Key,
                CapturedAt: frame.CapturedAt,
                GroupName: groupName,
                Image: clone);

            if (_options.DropWhenBusy)
            {
                // Queue will dispose dropped items (including ours if needed).
                _queue.TryEnqueue(item);
                return;
            }

            // Backpressure mode
            await _queue.EnqueueAsync(item, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Ensure we don't leak if cancellation happens mid-way.
            try { item?.Dispose(); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image logging failed for {Key}", frame.Key);
            try { item?.Dispose(); } catch { }
        }
    }

    private string ResolveGroupName(string cameraId)
    {
        if (_cameraToGroup.TryGetValue(cameraId, out var group))
            return group;

        return string.IsNullOrWhiteSpace(_options.UnknownGroupName)
            ? "_Unknown"
            : _options.UnknownGroupName;
    }

    private static Dictionary<string, string> BuildCameraGroupMap(ImageLoggingOptions options)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in options.Groups)
        {
            if (string.IsNullOrWhiteSpace(g.GroupName))
                continue;

            foreach (var cam in g.CameraIds)
            {
                if (string.IsNullOrWhiteSpace(cam))
                    continue;

                map[cam] = g.GroupName.Trim();
            }
        }

        return map;
    }
}
