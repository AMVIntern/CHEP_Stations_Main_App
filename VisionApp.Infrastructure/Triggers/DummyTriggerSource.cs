using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Triggers;

/// <summary>
/// Dummy trigger source to validate the pipeline without a PLC.
/// Emits a single cycle worth of triggers in the configured order.
/// Later replace with a libplctag-based PLC trigger source.
/// </summary>
public sealed class DummyTriggerSource : ITriggerSource
{
    private readonly ILogger<DummyTriggerSource> _logger;
    public DummyTriggerSource(ILogger<DummyTriggerSource> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<TriggerEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // One full Station1 cycle: Cam1..Cam4 at trigger indexes 1..5
        var keys = new[]
        {
            new TriggerKey("Cam1", 1),
            new TriggerKey("Cam2", 1),
            new TriggerKey("Cam3", 1),
            new TriggerKey("Cam4", 1),

            new TriggerKey("Cam1", 2),
            new TriggerKey("Cam2", 2),
            new TriggerKey("Cam3", 2),
            new TriggerKey("Cam4", 2),

            new TriggerKey("Cam1", 3),
            new TriggerKey("Cam2", 3),
            new TriggerKey("Cam3", 3),
            new TriggerKey("Cam4", 3),

            new TriggerKey("Cam1", 4),
            new TriggerKey("Cam2", 4),
            new TriggerKey("Cam3", 4),
            new TriggerKey("Cam4", 4),

            new TriggerKey("Cam1", 5),
            new TriggerKey("Cam2", 5),
            new TriggerKey("Cam3", 5),
            new TriggerKey("Cam4", 5),
        };

        // Repeat cycles so you can see multiple completions while the app is running.
        var cycleNumber = 0;

        while (!ct.IsCancellationRequested)
        {
            cycleNumber++;
            _logger.LogInformation("=== Starting dummy cycle #{Cycle} ===", cycleNumber);

            foreach (var key in keys)
            {
                // Simulate PLC timing gaps
                await Task.Delay(1000, ct);
                var ev = new TriggerEvent(key, DateTimeOffset.UtcNow);

                _logger.LogInformation("Trigger -> {Trigger}", ev);
                yield return ev;
            }

            // Small gap between products
            await Task.Delay(5000, ct);
        }
    }
}
