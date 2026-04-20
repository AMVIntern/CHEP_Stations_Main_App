using HalconDotNet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VisionApp.Core.Domain;

namespace VisionApp.Infrastructure.Logging;

public sealed class ImageLogWriterService : BackgroundService
{
    private readonly ImageLoggingOptions _options;
    private readonly ImageLogQueue _queue;
    private readonly ILogger<ImageLogWriterService> _logger;

    // CycleId -> folder info
    private readonly ConcurrentDictionary<Guid, CycleFolderInfo> _cycleFolderMap = new();
    private readonly ConcurrentQueue<Guid> _cycleOrder = new();

    // Keep this bounded (pick a number that covers your worst-case “active” cycles)
    private const int MaxCycleFolders = 2000;

    private sealed record CycleFolderInfo(string MonthFolder, string DayFolder, string CycleFolderName);

    public ImageLogWriterService(
        ImageLoggingOptions options,
        ImageLogQueue queue,
        ILogger<ImageLogWriterService> logger)
    {
        _options = options;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("ImageLogWriterService disabled (ImageLogging.Enabled=false).");
            return;
        }

        _logger.LogInformation("ImageLogWriterService started. Root={Root}", _options.RootFolder);

        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    WriteItem(item);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write image log item: {Item}", item);
                }
                finally
                {
                    item.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            _logger.LogInformation("ImageLogWriterService stopped.");
        }
    }

    private void WriteItem(ImageLogItem item)
    {
        if (item.Image is null || !item.Image.IsInitialized())
            return;

        var fmt = NormalizeFormat(_options.Format);
        var ext = NormalizeExtension(fmt);

        var safeGroup = SanitizePathPart(item.GroupName);

        var info = GetOrCreateCycleFolderInfo(item.CycleId, item.CapturedAt);

        var baseDir = Path.Combine(_options.RootFolder, safeGroup);

        if (_options.UseMonthSubfolder)
            baseDir = Path.Combine(baseDir, info.MonthFolder);

        if (_options.UseDaySubfolder)
            baseDir = Path.Combine(baseDir, info.DayFolder);

        baseDir = Path.Combine(baseDir, info.CycleFolderName);

        if (_options.IncludeCameraSubfolder)
        {
            var safeCam = SanitizePathPart(item.Key.CameraId);
            baseDir = Path.Combine(baseDir, safeCam);
        }

        Directory.CreateDirectory(baseDir);

        var fileName = BuildFileName(item.Key, item.CapturedAt, ext);
        var fullPath = Path.Combine(baseDir, fileName);

        var quality = fmt is "jpeg" ? _options.JpegQuality : 0;
        HOperatorSet.WriteImage(item.Image, fmt, quality, fullPath);
    }

    private CycleFolderInfo GetOrCreateCycleFolderInfo(Guid cycleId, DateTimeOffset firstSeenCapturedAt)
    {
        bool created = false;

        var info = _cycleFolderMap.GetOrAdd(cycleId, _ =>
        {
            created = true;

            var local = firstSeenCapturedAt.ToLocalTime();

            var monthFolder = local.ToString("MM"); // 01..12
            var dayFolder = local.ToString("dd");   // 01..31  (change if you prefer yyyy-MM-dd)

            var ts = local.ToString("yyyyMMdd_HHmmss_fff");

            if (_options.IncludeCycleIdInFolder)
            {
                var shortId = cycleId.ToString("N")[..8];
                return new CycleFolderInfo(monthFolder, dayFolder, $"{ts}__{shortId}");
            }

            return new CycleFolderInfo(monthFolder, dayFolder, ts);
        });

        if (created)
        {
            _cycleOrder.Enqueue(cycleId);
            TrimCycleFolderMap();
        }

        return info;
    }

    private void TrimCycleFolderMap()
    {
        while (_cycleFolderMap.Count > MaxCycleFolders && _cycleOrder.TryDequeue(out var old))
        {
            _cycleFolderMap.TryRemove(old, out _);
        }
    }

    private static string BuildFileName(TriggerKey key, DateTimeOffset capturedAt, string ext)
    {
        var local = capturedAt.ToLocalTime();
        var ts = local.ToString("yyyyMMdd_HHmmss_fff");
        return $"{SanitizePathPart(key.CameraId)}_T{key.Index:D2}_{ts}.{ext}";
    }

    private static string NormalizeFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "png";

        return format.Trim().ToLowerInvariant() switch
        {
            "jpg" => "jpeg",
            "jpeg" => "jpeg",
            "png" => "png",
            "bmp" => "bmp",
            "tif" => "tiff",
            "tiff" => "tiff",
            _ => "png"
        };
    }

    private static string NormalizeExtension(string normalizedFormat)
        => normalizedFormat == "jpeg" ? "jpg" : normalizedFormat;

    private static string SanitizePathPart(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "_";

        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');

        return input.Trim();
    }
}
