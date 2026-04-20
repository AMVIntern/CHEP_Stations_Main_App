namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public sealed class ShiftOptions
{
	public const string SectionName = "Shift";

	// Defaults (edit to match your client)
	public TimeSpan Shift1Start { get; init; } = new(6, 0, 0);   // 06:00
	public TimeSpan Shift2Start { get; init; } = new(14, 0, 0);  // 14:00
	public TimeSpan Shift3Start { get; init; } = new(22, 0, 0);  // 22:00

	// Your site timezone
	public string TimeZoneId { get; init; } = "Australia/Melbourne";
}
