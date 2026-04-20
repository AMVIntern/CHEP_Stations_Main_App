using VisionApp.Infrastructure.Inspection.Steps.Filters;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public sealed class Station5DefectAssignmentOptions
{
	public const string SectionName = "Station5DefectAssignment";

	public string InputOutputKey { get; init; } = "YoloX_Filtered";

	/// <summary>
	/// Ordered list of model class labels (index -> label).
	/// MUST match the model's class order.
	/// </summary>
	public string[] ClassLabels { get; init; } = Array.Empty<string>();

	// Provision: map model output labels to your report group codes
	public Dictionary<string, string> LabelToGroup { get; init; } =
		new(StringComparer.OrdinalIgnoreCase);

	public List<Station5DefectAssignmentStationOptions> Stations { get; init; } = new();

	/// <summary>
	/// Per-trigger-index vertical band filter rules (key = trigger index).
	/// If a trigger index has no entry the filter defaults to KeepAll (pass-through).
	/// </summary>
	public Dictionary<int, VerticalBandRuleOptions> VerticalBandRules { get; init; } = new();

	/// <summary>
	/// Fallback confidence threshold for any class not listed in ClassThresholds.
	/// </summary>
	public double DefaultThreshold { get; init; } = 0.80;

	/// <summary>
	/// Per-class confidence thresholds for YoloX detection filtering.
	/// Key = class label (case-insensitive). Classes not listed fall back to DefaultThreshold.
	/// </summary>
	public Dictionary<string, double> ClassThresholds { get; init; }
		= new(StringComparer.OrdinalIgnoreCase);
}

public sealed class Station5DefectAssignmentStationOptions
{
	public string StationKey { get; init; } = string.Empty;

	// "1": "B1", "3": "B2", "5": "B3"
	public Dictionary<int, string> BearerByTriggerIndex { get; init; } = new();

	// "S5Cam1": ["TLB1", "TIB1"], can be 2/3/4 elements
	public Dictionary<string, string[]> CameraElements { get; init; } = new();

	// PN becomes TN for triggers 1..N (you said 1..4)
	public int PnToTn_MaxTriggerIndex { get; init; } = 4;
}
