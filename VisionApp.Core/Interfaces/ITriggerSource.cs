using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

/// <summary>
/// Source of trigger events (typically PLC tags, but can also be replay/simulation).
/// Implementations should yield triggers in the order they are observed.
/// </summary>
public interface ITriggerSource
{
    IAsyncEnumerable<TriggerEvent> ReadAllAsync(CancellationToken ct);
}
