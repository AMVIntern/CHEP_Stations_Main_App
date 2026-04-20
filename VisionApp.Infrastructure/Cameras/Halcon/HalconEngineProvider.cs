using HalconDotNet;
using Microsoft.Extensions.Logging;

namespace VisionApp.Infrastructure.Cameras.Halcon;

public sealed class HalconEngineProvider : IDisposable
{
    public HDevEngine Engine { get; }

    public HalconEngineProvider(HalconProcedureOptions opts, ILogger<HalconEngineProvider> logger)
    {
        if (string.IsNullOrWhiteSpace(opts.ProcedurePath))
            throw new InvalidOperationException("HalconProcedures.ProcedurePath is not configured.");

        Engine = new HDevEngine();

        var full = Path.GetFullPath(opts.ProcedurePath);
        Engine.SetProcedurePath(full);

        logger.LogInformation("HALCON procedure path set to: {Path}", full);
    }

    public void Dispose()
    {
        try { Engine.Dispose(); } catch { }
    }
}
