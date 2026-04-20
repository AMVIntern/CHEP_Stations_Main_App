namespace VisionApp.Infrastructure.Sinks;

public sealed class Station5DefectReportCsvOptions
{
	public const string SectionName = "Station5DefectReportCsv";

	public bool Enabled { get; init; } = true;

	public string RootFolder { get; init; } = @"C:\VisionAppLogs\Station5Defects";

	// Creates:  S1_Report_Shift_1_29-Jan-2026.csv
	public string FileNamePattern { get; init; } = "{Station}_Report_Shift_{Shift}_{FileDate}.csv";

	// File date: 29-Jan-2026
	public string FileDateFormat { get; init; } = "dd-MMM-yyyy";

	// Row date: 29-Jan-26
	public string RowDateFormat { get; init; } = "dd-MMM-yy";

	// Timestamp like: 6:18:31
	public string RowTimeFormat { get; init; } = "H:mm:ss";

	public bool UseStationSubfolder { get; init; } = false;

	// Optional: mimic your sample having thousands of empty lines.
	public int PadBlankLinesAfterWrite { get; init; } = 0;

	public string[] DefectGroups { get; init; } = Array.Empty<string>();
	public string[] Elements { get; init; } = Array.Empty<string>();

}
