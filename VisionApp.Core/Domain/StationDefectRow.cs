namespace VisionApp.Core.Domain;

public sealed record StationDefectRow(
	Guid CycleId,
	string StationKey,
	DateOnly Date,          // business/shift date — for file naming and shift grouping
	DateOnly CalendarDate,  // actual wall-clock date — for the CSV Date column
	TimeOnly Timestamp,
	int Shift,
	IReadOnlyDictionary<string, int> Counts
);
