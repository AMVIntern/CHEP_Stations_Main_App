namespace VisionApp.Core.Domain;

/// <summary>
/// Result of shift resolution: Shift number + the business date for that shift.
/// </summary>
public readonly record struct ShiftInfo(int ShiftNumber, DateOnly ShiftDate, DateOnly CalendarDate);
