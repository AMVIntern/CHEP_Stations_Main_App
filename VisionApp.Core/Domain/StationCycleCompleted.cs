namespace VisionApp.Core.Domain;

public sealed record StationCycleCompleted(
	Guid CycleId,
	string StationKey,
	bool OverallPass,
	IReadOnlyDictionary<string, double> Metrics,
	DateTimeOffset CompletedAt);
