using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection.Runners;

public sealed class TriggerKeyInspectionRouter : IInspectionRunner
{
    private readonly IReadOnlyDictionary<TriggerKey, IInspectionRunner> _byKey;
    private readonly IReadOnlyDictionary<string, IInspectionRunner> _byCamera;
    private readonly IInspectionRunner _fallback;
    private readonly ILogger<TriggerKeyInspectionRouter> _logger;

    public TriggerKeyInspectionRouter(
        IReadOnlyDictionary<TriggerKey, IInspectionRunner> byKey,
        IReadOnlyDictionary<string, IInspectionRunner> byCamera,
        IInspectionRunner fallback,
        ILogger<TriggerKeyInspectionRouter> logger)
    {
        _byKey = byKey;
        _byCamera = byCamera;
        _fallback = fallback;
        _logger = logger;
    }

    public Task<InspectionResult> InspectAsync(FrameArrived frame, CancellationToken ct)
    {
        if (_byKey.TryGetValue(frame.Key, out var r1))
            return r1.InspectAsync(frame, ct);

        if (_byCamera.TryGetValue(frame.Key.CameraId, out var r2))
            return r2.InspectAsync(frame, ct);

        _logger.LogDebug("No inspection configured for {Key}; using fallback.", frame.Key);
        return _fallback.InspectAsync(frame, ct);
    }
}
