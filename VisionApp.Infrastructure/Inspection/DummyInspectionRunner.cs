using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection;

/// <summary>
/// Dummy inspection runner that always returns PASS.
/// Later replace with real HALCON + ONNX Runtime inference pipeline.
/// </summary>
public sealed class DummyInspectionRunner : IInspectionRunner
{
    public async Task<InspectionResult> InspectAsync(FrameArrived frame, CancellationToken ct)
    {
        // Simulate a little bit of processing time
        await Task.Delay(10, ct);

        return new InspectionResult(
            CycleId: frame.CycleId,
            Key: frame.Key,
            Pass: true,
            Score: 0.99,
            Message: "Dummy inspection passed",
            Metrics: null,
            Visuals: null,
			CompletedAt: DateTimeOffset.UtcNow);
    }
}
