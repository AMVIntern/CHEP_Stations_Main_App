using VisionApp.Infrastructure.Inspection.Models;
using VisionApp.Infrastructure.Inspection.Pipeline;

namespace VisionApp.Infrastructure.Inspection.Steps;

public sealed record FinalDecision(bool Pass, double Score, string Message);

public static class InspectionKeys
{
    public const string Final = "__final";
}

public sealed class DecisionStep : IInspectionStep
{
    public string Name => "Decision";

    private readonly string[] _requiredOutputs;

    public DecisionStep(params string[] requiredOutputs)
    {
        _requiredOutputs = requiredOutputs;
    }

    public Task ExecuteAsync(InspectionContext context, CancellationToken ct)
    {
        // Example: if ANY required output fails => fail
        var allPass = true;
        double score = 1.0;
        var messages = new List<string>();

        foreach (var key in _requiredOutputs)
        {
            if (!context.Items.TryGetValue(key, out var obj))
            {
                allPass = false;
                messages.Add($"{key}: missing output");
                continue;
            }

            if (obj is YoloXOutput y)
            {
                if (!y.Passed) allPass = false;
                score = Math.Min(score, y.Confidence);
                if (!y.Passed) messages.Add($"{key}: defects={y.ClassCounts}");
            }

            // Add BarcodeOutput / HalconOutput cases later
        }

        context.Items[InspectionKeys.Final] = new FinalDecision(
            allPass,
            score,
            allPass ? "OK" : string.Join("; ", messages));

        return Task.CompletedTask;
    }
}


