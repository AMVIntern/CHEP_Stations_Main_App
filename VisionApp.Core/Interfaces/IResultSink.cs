using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

/// <summary>
/// Writes final cycle results somewhere (PLC tags, CSV, SQL, API, etc.).
/// You can register multiple sinks (e.g., PLC + CSV) and fan out later.
/// </summary>
public interface IResultSink
{
    Task WriteCycleResultAsync(CycleCompleted completed, CancellationToken ct);
}
