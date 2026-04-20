using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection.Runners;

public sealed class CameraInspectionRouter : IInspectionRunner
{
    private readonly IReadOnlyDictionary<string, IInspectionRunner> _map;
    private readonly IInspectionRunner _fallback;
    private readonly ILogger<CameraInspectionRouter> _logger;

    public CameraInspectionRouter(
        IReadOnlyDictionary<string, IInspectionRunner> map,
        IInspectionRunner fallback,
        ILogger<CameraInspectionRouter> logger)
    {
        _map = map;
        _fallback = fallback;
        _logger = logger;
    }

    public Task<InspectionResult> InspectAsync(FrameArrived frame, CancellationToken ct)
    {
        if (_map.TryGetValue(frame.Key.CameraId, out var runner))
            return runner.InspectAsync(frame, ct);

        _logger.LogDebug("No inspection pipeline configured for {CameraId}; using fallback.", frame.Key.CameraId);
        return _fallback.InspectAsync(frame, ct);
    }
}
