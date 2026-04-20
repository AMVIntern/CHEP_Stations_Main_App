namespace VisionApp.Core.Domain;

public sealed record InspectionResult(
	Guid CycleId,
	TriggerKey Key,
	bool Pass,
	double Score,
	string? Message,
	IReadOnlyDictionary<string, double>? Metrics,
	InspectionVisuals? Visuals,
	DateTimeOffset CompletedAt,
	int ImageWidth = 0,
	int ImageHeight = 0)
{
	public override string ToString()
		=> $"{CompletedAt:O}  Cycle={CycleId}  {Key}  Pass={Pass}  Score={Score:0.###}  {Message}";
}
