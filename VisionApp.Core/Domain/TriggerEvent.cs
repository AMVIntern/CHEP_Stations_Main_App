namespace VisionApp.Core.Domain;

/// <summary>
/// A trigger event observed by the application (typically sourced from PLC tags).
/// </summary>
public sealed record TriggerEvent(
    TriggerKey Key,
    DateTimeOffset Timestamp)
{
    public override string ToString() => $"{Timestamp:O}  {Key}";
}

