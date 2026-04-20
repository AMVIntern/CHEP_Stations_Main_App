using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Core.Interfaces;

namespace VisionApp.Core.Hosting.Services;

/// <summary>
/// Single-threaded orchestrator for the cycle lifecycle:
/// - Consumes TriggerEvent
/// - Produces CaptureRequest
/// - Consumes InspectionResult
/// - Produces CycleCompleted (fan-out to sinks; v1 is a simple hook)
///
/// Note: This service runs the "truth" of the cycle. Keep it deterministic.
/// </summary>
public sealed class CycleEngineService : BackgroundService
{
    private readonly CycleEngine _engine;

    private readonly ChannelReader<TriggerEvent> _triggerReader;
    private readonly ChannelWriter<CaptureRequest> _captureWriter;

    private readonly ChannelReader<InspectionResult> _inspectionReader;

    private readonly IEnumerable<IResultSink> _resultSinks;
    private readonly ILogger<CycleEngineService> _logger;

    public CycleEngineService(
        CycleEngine engine,
        Channel<TriggerEvent> triggerChannel,
        Channel<CaptureRequest> captureChannel,
        Channel<InspectionResult> inspectionChannel,
        IEnumerable<IResultSink> resultSinks,
        ILogger<CycleEngineService> logger)
    {
        _engine = engine;

        _triggerReader = triggerChannel.Reader;
        _captureWriter = captureChannel.Writer;

        _inspectionReader = inspectionChannel.Reader;

        _resultSinks = resultSinks;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // We want to process BOTH:
        // - triggers (start/progress cycle)
        // - inspection results (complete cycle)
        //
        // Two read loops is fine because CycleEngine itself is not thread-safe.
        // So: we serialize access using a single gate lock.

        _logger.LogInformation("CycleEngineService started.");
        var gate = new SemaphoreSlim(1, 1);

        var t1 = Task.Run(() => TriggerLoopAsync(gate, ct), ct);
        var t2 = Task.Run(() => InspectionLoopAsync(gate, ct), ct);

        try
        {
            await Task.WhenAll(t1, t2);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        finally
        {
            _logger.LogInformation("CycleEngineService stopped.");
        }
    }

    private async Task TriggerLoopAsync(SemaphoreSlim gate, CancellationToken ct)
    {
        try
        {
            await foreach (var trigger in _triggerReader.ReadAllAsync(ct))
            {
                await gate.WaitAsync(ct);
                try
                {
                    if (_engine.TryHandleTrigger(trigger, out var request) && request != null)
                    {
                        _logger.LogInformation("CycleEngine accepted trigger {Trigger} -> emit {Req}", trigger, request);
                        await _captureWriter.WriteAsync(request, ct);
                    }
                    else
                    {
                        _logger.LogDebug("CycleEngine ignored trigger {Trigger}", trigger);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task InspectionLoopAsync(SemaphoreSlim gate, CancellationToken ct)
    {
        try
        {
            await foreach (var result in _inspectionReader.ReadAllAsync(ct))
            {
                await gate.WaitAsync(ct);
                try
                {
                    if (_engine.TryHandleInspectionResult(result, out var completed) && completed != null)
                    {
                        _logger.LogInformation(
                            "=== CycleCompleted: {CycleId} OverallPass={Pass} Results={Count} ===",
                            completed.CycleId, completed.OverallPass, completed.Results.Count);
                        // Fan out to result sinks (PLC, CSV, SQL, etc.)
                        foreach (var sink in _resultSinks)
                        {
                            await sink.WriteCycleResultAsync(completed, ct);
                        }
                    }
                }
                finally
                {
                    gate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
