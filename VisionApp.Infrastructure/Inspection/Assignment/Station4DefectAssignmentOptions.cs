using VisionApp.Infrastructure.Inspection.Steps.Filters;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

/// <summary>
/// Station 4 defect assignment configuration:
/// - element is determined by trigger index (not X segmentation)
/// - element is camera-prefixed (Cam1_B1, Cam2_B1-B2, etc)
/// </summary>
public sealed class Station4DefectAssignmentOptions
{
	public const string SectionName = "Station4DefectAssignment";

	public string StationKey { get; init; } = "Station4";

	/// <summary>
	/// Which step's boxes should be used for counting.
	/// Station4PipelineBuilder currently emits "YoloX".
	/// </summary>
	public string InputOutputKey { get; init; } = "YoloX";

	/// <summary>
	/// Column group order in the CSV (must match the group codes you emit in keys).
	/// </summary>
	public string[] DefectGroups { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Ordered list of model class labels (index -> label).
	/// MUST match the model's class order.
	/// </summary>
	public string[] ClassLabels { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Class labels that the pipeline should silently ignore.
	/// Matching detections produce no bounding box, do not affect pass/fail, and are
	/// excluded from counts. Intended for "GOOD" / background model classes.
	/// </summary>
	public string[] IgnoreLabels { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Optional mapping from model label -> group code (e.g. "EndPlateDamage" -> "EPD").
	/// If your pipeline already labels boxes as EPD/CT/TN, you can leave this empty.
	/// </summary>
	public Dictionary<string, string> LabelToGroup { get; init; }
		= new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Per-camera layout mapping for Station4.
	/// Cameras["S4Cam1"].TriggerElements[2] = "B1-B2"
	/// Prefixes are used for CSV columns: "Cam1_B1-B2".
	/// </summary>
	public Dictionary<string, Station4CameraLayout> Cameras { get; init; }
		= new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Maps raw trigger elements to PLC element names for the PLC write path.
	/// Composite elements collapse to their bearer: "B1-B2" → "B2", "B2-B3" → "B2".
	/// If empty, Station4 results are not written to PLC.
	/// </summary>
	public Dictionary<string, string> PlcTriggerElementMap { get; init; }
		= new(StringComparer.OrdinalIgnoreCase);

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

public sealed class Station4CameraLayout
{
	/// <summary>
	/// e.g. "Cam1" / "Cam2". If empty, we derive from CameraId suffix (S4Cam1 -> Cam1).
	/// </summary>
	public string Prefix { get; init; } = string.Empty;

	/// <summary>
	/// Trigger index -> element name, e.g. 1="B1", 2="B1-B2", 3="B2-B3", 4="B3"
	/// </summary>
	public Dictionary<int, string> TriggerElements { get; init; } = new();
}
