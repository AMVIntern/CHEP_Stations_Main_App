using HalconDotNet;
using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Cameras.Halcon;

/// <summary>
/// Real HALCON camera implementation.
/// - Opens HALCON framegrabber via HDev procedures
/// - GrabImageAsync on demand per capture request
/// - Reconnects on failure using backoff
///
/// NOTE:
/// All camera parameter setup (TriggerMode, StreamBufferHandlingMode, grab_timeout, etc.)
/// is handled inside your HALCON procedure: StartCameraFrameGrabber.
/// </summary>
public sealed class HalconGigECamera : ICamera, IDisposable
{
    private readonly HalconCameraConfig _cfg;
    private readonly HalconCameraOptions _opts;
    private readonly IFramegrabberFactory _factory;
    private readonly HalconOpenCloseGate _openCloseGate;
    private readonly ILogger<HalconGigECamera> _logger;

    private readonly SemaphoreSlim _grabLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private HTuple? _acqHandle;
    private volatile bool _connected;

    public string CameraId => _cfg.CameraId;

    // Hardcoded defaults (kept simple on purpose)
    private const int GrabMaxDelayMs = 0;          // 0 matches your old behavior
    private const int RetryDelayMs = 5;            // tiny delay before retry
    private const int BackoffStartMs = 250;
    private const int BackoffMaxMs = 2000;

    public HalconGigECamera(
        HalconCameraConfig cfg,
        HalconCameraOptions opts,
        IFramegrabberFactory factory,
        HalconOpenCloseGate openCloseGate,
        ILogger<HalconGigECamera> logger)
    {
        _cfg = cfg;
        _opts = opts;
        _factory = factory;
        _openCloseGate = openCloseGate;
        _logger = logger;
    }

    public async Task<RawFrameArrived> CaptureAsync(CaptureRequest request, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct).ConfigureAwait(false);

        if (_acqHandle is null)
            throw new InvalidOperationException($"Camera '{CameraId}' has no valid HALCON handle.");

        await _grabLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Drain any stale frames captured before this PLC edge
            DrainQueue(_acqHandle);

            var (ok, img) = await TryGrabAsync(_acqHandle, request.Key.Index, ct).ConfigureAwait(false);

            //if (!ok || img is null)
            //    throw new InvalidOperationException($"HALCON grab failed for {CameraId} trigger={request.Key.Index}");
            if (!ok || img is null)
            {
                _logger.LogWarning("HALCON grab failed for {CameraId} trigger={Trigger}. Returning empty frame.",
                    CameraId, request.Key.Index);

                return new RawFrameArrived(
                    CycleId: request.CycleId,
                    Key: request.Key,
                    Image: new HImage(),                 // uninitialized placeholder
                    CapturedAt: DateTimeOffset.UtcNow);
            }

            return new RawFrameArrived(
                CycleId: request.CycleId,
                Key: request.Key,
                Image: img,
                CapturedAt: DateTimeOffset.UtcNow);
        }
        finally
        {
            _grabLock.Release();
        }
    }

    public bool IsAlive()
    {
        if (_acqHandle is null) return false;

        try
        {
            HOperatorSet.GetFramegrabberParam(_acqHandle, "DeviceTemperature", out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connected && IsAlive())
            return;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connected && IsAlive())
                return;

            _connected = false;

            // Best-effort close old handle
            TryCloseHandle();

            int backoff = BackoffStartMs;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_opts.SerializeOpenClose)
                        await _openCloseGate.Gate.WaitAsync(ct).ConfigureAwait(false);

                    try
                    {
                        _acqHandle = _factory.Open(_cfg.CameraName);

                        // Verify new handle is usable
                        HOperatorSet.GetFramegrabberParam(_acqHandle, "DeviceTemperature", out _);

                        _connected = true;
                        _logger.LogInformation("Camera {CameraId} connected OK (CameraName={CameraName})",
                            CameraId, _cfg.CameraName);

                        return;
                    }
                    finally
                    {
                        if (_opts.SerializeOpenClose)
                            _openCloseGate.Gate.Release();
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Camera {CameraId} open failed, retrying in {Backoff}ms...",
                        CameraId, backoff);
                }

                if (ct.WaitHandle.WaitOne(backoff))
                    ct.ThrowIfCancellationRequested();

                backoff = Math.Min(backoff * 2, BackoffMaxMs);
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private void TryCloseHandle()
    {
        if (_acqHandle is null)
            return;

        try
        {
            if (_opts.SerializeOpenClose)
                _openCloseGate.Gate.Wait();

            try
            {
                _factory.Close(_acqHandle);
            }
            finally
            {
                if (_opts.SerializeOpenClose)
                    _openCloseGate.Gate.Release();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _acqHandle = null;
            _connected = false;
        }
    }

    private void DrainQueue(HTuple acqHandle)
    {
        try
        {
            while (true)
            {
                HOperatorSet.GetFramegrabberParam(acqHandle, "image_available", out HTuple ready);

                if (ready.Length > 0 && ready[0].I == 1)
                {
                    HObject junk;
                    HOperatorSet.GrabImageAsync(out junk, acqHandle, 0);
                    junk.Dispose();
                }
                else
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DrainQueue exception (safe to ignore)");
        }
    }

    private async Task<(bool success, HImage? img)> TryGrabAsync(
        HTuple acqHandle,
        int expectedFrame,
        CancellationToken ct)
    {
        try
        {
            // Dispose imgObj explicitly: HObject and HImage hold separate HALCON
            // reference-counted handles. Leaving HObject for the finalizer adds
            // HALCON-side GC pressure at high frame rates.
            HObject imgObj;
            HOperatorSet.GrabImageAsync(out imgObj, acqHandle, GrabMaxDelayMs);
            var img = new HImage(imgObj);
            imgObj.Dispose();
            return (true, img);
        }
        catch (HalconException hex) when (hex.GetErrorCode() == 5322)
        {
            // Simple single retry, matching your old pattern
            try
            {
                await Task.Delay(RetryDelayMs, ct).ConfigureAwait(false);

                HObject imgObj2;
                HOperatorSet.GrabImageAsync(out imgObj2, acqHandle, GrabMaxDelayMs);
                var img2 = new HImage(imgObj2);
                imgObj2.Dispose();

                _logger.LogDebug("Retry OK after 5322 for {CameraId} expectedFrame={Frame}",
                    CameraId, expectedFrame);

                return (true, img2);
            }
            catch
            {
                _logger.LogWarning("Retry failed after 5322 for {CameraId} expectedFrame={Frame}",
                    CameraId, expectedFrame);

                return (false, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Grab failed for {CameraId} expectedFrame={Frame}", CameraId, expectedFrame);
            return (false, null);
        }
    }

    public void Dispose()
    {
        TryCloseHandle();
        _grabLock.Dispose();
        _connectLock.Dispose();
    }
}
