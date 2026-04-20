using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Core.Hosting.Services;

public sealed class FramePreprocessService : BackgroundService
{
    private readonly ChannelReader<RawFrameArrived> _rawReader;
    private readonly ChannelWriter<FrameArrived> _processedWriter;

    private readonly IFramePreprocessor _preprocessor;
    private readonly IFrameObserver[] _observers;
    private readonly IImageLogger[] _loggers;

    private readonly ILogger<FramePreprocessService> _logger;

    public FramePreprocessService(
        Channel<RawFrameArrived> rawFrameChannel,
        Channel<FrameArrived> processedFrameChannel,
        IFramePreprocessor preprocessor,
        IEnumerable<IFrameObserver> observers,
        IEnumerable<IImageLogger> loggers,
        ILogger<FramePreprocessService> logger)
    {
        _rawReader = rawFrameChannel.Reader;
        _processedWriter = processedFrameChannel.Writer;
        _preprocessor = preprocessor;
        _observers = (observers ?? Array.Empty<IFrameObserver>()).ToArray();
        _loggers = (loggers ?? Array.Empty<IImageLogger>()).ToArray();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("FramePreprocessService started.");

        try
        {
            await foreach (var raw in _rawReader.ReadAllAsync(ct))
            {
                FrameArrived? processed = null;
                try
                {
                    processed = await _preprocessor.PreprocessAsync(raw, ct);

                    // Notify UI observers — each is isolated so one failure never
                    // silences the rest or breaks the downstream pipeline.
                    foreach (var obs in _observers)
                    {
                        try
                        {
                            await obs.OnFrameArrivedAsync(processed, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Frame observer {Observer} failed for {Key}",
                                obs.GetType().Name, processed.Key);
                        }
                    }

                    // Notify image loggers — same isolation.
                    foreach (var lg in _loggers)
                    {
                        try
                        {
                            await lg.LogFrameAsync(processed, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Image logger {Logger} failed for {Key}",
                                lg.GetType().Name, processed.Key);
                        }
                    }

                    // Hand off to inspection pipeline
                    await _processedWriter.WriteAsync(processed, ct);

                    // ownership transferred to downstream consumer
                    processed = null;
                }
                finally
                {
                    // ✅ Always dispose raw (we've cloned into processed)
                    raw.Dispose();

                    // ✅ If we failed before handing off, clean up processed
                    processed?.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            _logger.LogInformation("FramePreprocessService stopped.");
        }
    }

}
