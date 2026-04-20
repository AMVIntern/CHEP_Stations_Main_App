namespace VisionApp.Infrastructure.Inspection.Options;

public sealed class Station5DefectAssignmentStationOptions
{
	public const string SectionName = "StationDefectAssignment";

	public List<StationSpec> Stations { get; init; } = new();

	public StationSpec? TryGet(string stationKey)
		=> Stations.FirstOrDefault(s => string.Equals(s.StationKey, stationKey, StringComparison.OrdinalIgnoreCase));
}

public sealed class StationSpec
{
	public required string StationKey { get; init; }

	// CameraId -> ordered board elements visible left->right (2..4, sometimes 3)
	public Dictionary<string, List<string>> CameraBoards { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	// TriggerIndex -> bearer element name (B1/B2/B3 etc)
	public Dictionary<int, string> BearerByTriggerIndex { get; init; } = new();

	// Which element axis a defect uses ("Board" or "Bearer")
	public Dictionary<string, string> DefectTarget { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	// Rename rules: PN->TN for certain trigger indexes, etc.
	public List<RenameRule> RenameRules { get; init; } = new();
}

public sealed class RenameRule
{
	public required string From { get; init; }
	public required string To { get; init; }
	public HashSet<int> TriggerIndexes { get; init; } = new();
}
