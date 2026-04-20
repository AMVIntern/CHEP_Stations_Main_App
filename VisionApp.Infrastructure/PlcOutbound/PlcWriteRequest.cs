namespace VisionApp.Infrastructure.PlcOutbound;

/// <summary>
/// Base type for all PLC write queue entries.
/// </summary>
public abstract record PlcWriteEntry(string TagName);

/// <summary>
/// Writes a single boolean bit to a PLC tag.
/// </summary>
public sealed record PlcBoolWrite(string TagName, bool Value) : PlcWriteEntry(TagName);

/// <summary>
/// Writes a 32-bit signed integer (DINT) to a PLC tag.
/// </summary>
public sealed record PlcDintWrite(string TagName, int Value) : PlcWriteEntry(TagName);
