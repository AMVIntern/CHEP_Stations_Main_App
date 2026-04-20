using VisionApp.Core.Domain;

namespace VisionApp.Core.Engine;

/// <summary>
/// Static, recipe-defined definition of a product cycle:
/// - which triggers belong to a cycle
/// - which triggers can start the cycle
/// - which trigger ends the cycle
/// - the expected trigger order (for readability / validation)
/// </summary>
public sealed class CapturePlan
{
    public required IReadOnlyList<TriggerKey> OrderedTriggers { get; init; }
    public required HashSet<TriggerKey> StartTriggers { get; init; }
    public HashSet<TriggerKey> EndTriggers { get; init; } = new();
    /// <summary> Total number of trigger-based captures expected per cycle. </summary>
    public int ExpectedCount => OrderedTriggers.Count;
    public bool IsSingleTriggerCycle => OrderedTriggers.Count == 1 && IsEnd(OrderedTriggers[0]);
    public bool IsEnd(TriggerKey key) => EndTriggers.Contains(key);
    public bool IsStart(TriggerKey key) => StartTriggers.Contains(key);
    public bool Contains(TriggerKey key) => OrderedTriggers.Contains(key);
    public override string ToString()
        => $"CapturePlan: Expected={ExpectedCount}, Start=[{string.Join(", ", StartTriggers)}], End={EndTriggers}";
}
