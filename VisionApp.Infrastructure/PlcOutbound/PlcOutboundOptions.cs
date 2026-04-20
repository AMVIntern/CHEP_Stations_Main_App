namespace VisionApp.Infrastructure.PlcOutbound;

public sealed class PlcOutboundOptions
{
	public const string SectionName = "PlcOutbound";

	public bool Enabled { get; init; } = true;

	// Same PLC as triggers
	public string Gateway { get; init; } = "192.168.1.110";
	public string Path { get; init; } = "1,0";
	public int TimeoutMs { get; init; } = 2000;

	public PlcHeartbeatOptions Heartbeat { get; init; } = new();
	public PlcResultsOptions Results { get; init; } = new();
}

public sealed class PlcHeartbeatOptions
{
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Example placeholder: "resultsFromApp1.Heartbeat"
	/// </summary>
	public string TagName { get; init; } = "";

	/// <summary>
	/// Toggle interval in ms. The heartbeat flips true → false → true … every IntervalMs.
	/// Default 1000ms (1 second).
	/// </summary>
	public int IntervalMs { get; init; } = 1000;
}

public sealed class PlcResultsOptions
{
	public List<PlcStationResultsOptions> Stations { get; init; } = new();

	/// <summary>
	/// Maps internal label/group -> PLC defect leaf name.
	/// Example: "RN" -> "RaisedNails"
	/// </summary>
	public Dictionary<string, string> LabelToPlcDefect { get; init; } =
		new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlcStationResultsOptions
{
	public string StationKey { get; init; } = "";

	// e.g. "resultsFromApp1"
	public string BaseTag { get; init; } = "";

	// Elements that exist for this station (TLB1..B3 etc.)
	public List<string> Elements { get; init; } = new();

	// Defect leaf names on PLC side (RaisedNails, Staples, ...)
	public List<string> Defects { get; init; } = new();

	/// <summary>
	/// Sub-tag written as a DINT timestamp (HHmmss) after all bool defect tags.
	/// Written to {BaseTag}.{TimestampTagSuffix}. Leave empty to disable.
	/// </summary>
	public string TimestampTagSuffix { get; init; } = "";
}
