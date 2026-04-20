namespace VisionApp.Core.Domain;

/// <summary>
/// Final aggregate output for one completed product cycle.
/// Contains per-trigger inspection results and an overall pass/fail.
/// </summary>
public sealed record CycleCompleted(
    Guid CycleId,
    bool OverallPass,
    IReadOnlyList<InspectionResult> Results,
    DateTimeOffset CompletedAt)
{
    public override string ToString()
        => $"{CompletedAt:O}  Cycle={CycleId}  OverallPass={OverallPass}  Results={Results.Count}";
}

