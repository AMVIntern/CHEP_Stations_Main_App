namespace VisionApp.Infrastructure.Inspection.Models;

public sealed record ElementCountsOutput(
	string StationKey,
	IReadOnlyDictionary<string, int> Counts
);
