using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VisionApp.Infrastructure.Logging;

/// <summary>
/// Bounded channel queue for disk logging.
/// IMPORTANT: We must dispose dropped items ourselves (Channel DropOldest does NOT dispose).
/// </summary>
public sealed class ImageLogQueue
{
    private readonly ImageLoggingOptions _options;
    private readonly ILogger<ImageLogQueue> _logger;

    private readonly Channel<ImageLogItem> _channel;

    public ChannelReader<ImageLogItem> Reader => _channel.Reader;

    public ImageLogQueue(ImageLoggingOptions options, ILogger<ImageLogQueue> logger)
    {
        _options = options;
        _logger = logger;

        // IMPORTANT:
        // Use FullMode=Wait always.
        // If DropWhenBusy=true we implement drop-oldest manually in TryEnqueue (with disposal).
        var bounded = new BoundedChannelOptions(_options.QueueCapacity)
        {
            // We sometimes TryRead() from writers when dropping oldest,
            // so do NOT claim SingleReader=true.
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<ImageLogItem>(bounded);
    }

    /// <summary>
    /// Try to enqueue without blocking.
    /// If DropWhenBusy=true and queue is full, we drop OLDEST (and dispose it) then retry.
    /// </summary>
    public bool TryEnqueue(ImageLogItem item)
    {
        if (!_options.Enabled)
        {
            item.Dispose();
            return false;
        }

        // If we are NOT dropping when busy, this is a pure "try" method.
        // Caller can fall back to EnqueueAsync for backpressure.
        if (!_options.DropWhenBusy)
        {
            if (_channel.Writer.TryWrite(item))
                return true;

            return false;
        }

        // DropWhenBusy=true: never block. Try write.
        if (_channel.Writer.TryWrite(item))
            return true;

        // Queue is full. Drop oldest safely (dispose).
        if (_channel.Reader.TryRead(out var dropped))
        {
            try
            {
                _logger.LogWarning("Image log queue full. Dropping oldest: {Item}", dropped);
            }
            catch { /* logging should never break us */ }

            try { dropped.Dispose(); } catch { }
        }

        // Retry write after dropping one.
        if (_channel.Writer.TryWrite(item))
            return true;

        // Still full (very rare under contention) -> drop newest (dispose).
        try
        {
            _logger.LogWarning("Image log queue still full after drop. Dropping newest: {Item}", item);
        }
        catch { }

        item.Dispose();
        return false;
    }

    /// <summary>
    /// Enqueue and wait if full (DropWhenBusy=false).
    /// If DropWhenBusy=true, this behaves like TryEnqueue (never blocks).
    /// </summary>
    public async ValueTask EnqueueAsync(ImageLogItem item, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            item.Dispose();
            return;
        }

        if (_options.DropWhenBusy)
        {
            TryEnqueue(item); // will dispose if cannot enqueue
            return;
        }

        try
        {
            await _channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
        }
        catch
        {
            // If write fails/cancels, ensure we don't leak the image.
            try { item.Dispose(); } catch { }
            throw;
        }
    }

    public void Complete(Exception? ex = null)
    {
        _channel.Writer.TryComplete(ex);
    }
}
