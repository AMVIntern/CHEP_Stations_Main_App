using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Sinks;

/// <summary>
/// No-op result sink for v1 so the system can run without PLC/CSV/SQL output.
/// Replace with real sinks (PLC writeback, CSV writer, SQL writer, etc.).
/// </summary>
public sealed class NullResultSink : IResultSink
{
    public Task WriteCycleResultAsync(CycleCompleted completed, CancellationToken ct)
        => Task.CompletedTask;
}
