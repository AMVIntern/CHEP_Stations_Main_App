using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Core.Hosting.Services;

/// <summary>
/// Reads triggers from an ITriggerSource (PLC / replay / simulation)
/// and pushes them into the trigger channel.
/// </summary>
public sealed class TriggerPumpService : BackgroundService
{
    private readonly ITriggerSource _triggerSource;
    private readonly ChannelWriter<TriggerEvent> _writer;
    private readonly ILogger<TriggerPumpService> _logger;

    public TriggerPumpService(
        ITriggerSource triggerSource,
        Channel<TriggerEvent> triggerChannel,
        ILogger<TriggerPumpService> logger)
    {
        _triggerSource = triggerSource;
        _writer = triggerChannel.Writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TriggerPumpService started.");
        try
        {
            await foreach (var trigger in _triggerSource.ReadAllAsync(stoppingToken))
            {
                await _writer.WriteAsync(trigger, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        finally
        {
            _logger.LogInformation("TriggerPumpService stopped.");
        }
    }
}
