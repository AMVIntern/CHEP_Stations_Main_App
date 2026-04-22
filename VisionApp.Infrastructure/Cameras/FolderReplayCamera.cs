using HalconDotNet;
using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Cameras;

/// <summary>
/// Camera implementation that "captures" by reading images sequentially from a folder.
/// Each camera instance has its own file list and index.
/// </summary>
public sealed class FolderReplayCamera : ICamera
{
    private static readonly string[] DefaultExtensions =
        [".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff"];

    private readonly ILogger<FolderReplayCamera> _logger;
    private readonly FolderReplayOptions _options;

    private readonly string _folderPath;
    private readonly string[] _files;

    private int _nextIndex = 0;

    // Optional: guard against concurrent CaptureAsync calls for the same camera instance
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string CameraId { get; }

    public FolderReplayCamera(
        string cameraId,
        string folderPath,
        FolderReplayOptions options,
        ILogger<FolderReplayCamera> logger)
    {
        CameraId = cameraId;
        _folderPath = folderPath;
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_folderPath))
            throw new InvalidOperationException($"{nameof(FolderReplayOptions)} folder path is empty for camera '{cameraId}'.");

        if (!Directory.Exists(_folderPath))
            throw new DirectoryNotFoundException($"Replay folder not found for {cameraId}: '{_folderPath}'");

        _files = IndexFiles(_folderPath, _options.FilterExtensions);

        if (_files.Length == 0)
            throw new InvalidOperationException($"No image files found for {cameraId} in '{_folderPath}'.");

        _logger.LogInformation("FolderReplayCamera {Cam} initialized: {Count} files in {Folder}",
            CameraId, _files.Length, _folderPath);
    }

    public async Task<RawFrameArrived> CaptureAsync(CaptureRequest request, CancellationToken ct)
    {
        // Keep per-camera sequencing deterministic
        await _gate.WaitAsync(ct);
        try
        {
            var path = GetNextPathOrThrow();

            // Read the image with HALCON
            // Note: HImage.ReadImage will create an HImage that must be disposed later.
            var img = new HImage(path);

            if (!img.IsInitialized())
                throw new InvalidOperationException($"HALCON ReadImage returned uninitialized image for '{path}'.");

            _logger.LogDebug("FolderReplayCamera {Cam} -> {File}", CameraId, Path.GetFileName(path));

            return new RawFrameArrived(
                CycleId: request.CycleId,
                Key: request.Key,
                Image: img,
                CapturedAt: DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetNextPathOrThrow()
    {
        if (_files.Length == 0)
            throw new InvalidOperationException($"No files indexed for {CameraId}.");

        if (_nextIndex >= _files.Length)
        {
            if (_options.Loop)
            {
                _logger.LogInformation("FolderReplayCamera {Cam} reached end; looping.", CameraId);
                _nextIndex = 0;
            }
            else
            {
                throw new InvalidOperationException($"FolderReplayCamera {CameraId} reached end of file list and Loop=false.");
            }
        }

        return _files[_nextIndex++];
    }

    private static string[] IndexFiles(string folder, bool filterExtensions)
    {
        var all = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);

        IEnumerable<string> files = all;

        if (filterExtensions)
        {
            files = files.Where(f =>
            {
                var ext = Path.GetExtension(f);
                return DefaultExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            });
        }

        // Deterministic ordering: alphabetical by full path
        return files
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
