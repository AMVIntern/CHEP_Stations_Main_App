using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Core.Hosting.Services;

public sealed class CameraDispatcherService : BackgroundService
{
    private readonly IReadOnlyDictionary<string, ICamera> _camerasById;

    private readonly ChannelReader<CaptureRequest> _captureReader;
    private readonly ChannelWriter<RawFrameArrived> _frameWriter;

    private readonly ILogger<CameraDispatcherService> _logger;

    // Limit how many capture tasks can run at once (tune as needed)
    private readonly SemaphoreSlim _inflight;

    public CameraDispatcherService(
        IEnumerable<ICamera> cameras,
        Channel<CaptureRequest> captureChannel,
        Channel<RawFrameArrived> frameChannel,
        ILogger<CameraDispatcherService> logger)
    {
        _camerasById = cameras.ToDictionary(c => c.CameraId, c => c, StringComparer.OrdinalIgnoreCase);
        _captureReader = captureChannel.Reader;
        _frameWriter = frameChannel.Writer;
        _logger = logger;

        // Good default: allow up to number of cameras (or a sensible cap)
        var max = Math.Clamp(_camerasById.Count, 1, 8);
        _inflight = new SemaphoreSlim(max, max);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("CameraDispatcherService started. Cameras={Count}", _camerasById.Count);

        // List<Task> allows RemoveAll to prune completed tasks each iteration,
        // preventing unbounded accumulation over long production runs.
        var tasks = new List<Task>();

        try
        {
            await foreach (var request in _captureReader.ReadAllAsync(ct))
            {
                await _inflight.WaitAsync(ct).ConfigureAwait(false);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await CaptureOneAsync(request, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Capture task failed for {Req}", request);
                    }
                    finally
                    {
                        _inflight.Release();
                    }
                }, ct);

                tasks.Add(task);
                tasks.RemoveAll(t => t.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        finally
        {
            // Best-effort wait for outstanding capture tasks
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch { /* ignore */ }

            _logger.LogInformation("CameraDispatcherService stopped.");
        }
    }

    private async Task CaptureOneAsync(CaptureRequest request, CancellationToken ct)
    {
        if (!_camerasById.TryGetValue(request.Key.CameraId, out var camera))
            throw new InvalidOperationException($"No camera registered for CameraId '{request.Key.CameraId}'.");

        var frame = await camera.CaptureAsync(request, ct).ConfigureAwait(false);

        try
        {
            await _frameWriter.WriteAsync(frame, ct).ConfigureAwait(false);
            _logger.LogDebug("RawFrameArrived -> {Frame}", frame);
        }
        catch
        {
            // If we fail to enqueue, we must dispose to avoid leaking HALCON objects
            frame.Dispose();
            throw;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _inflight.Dispose();
    }
}
