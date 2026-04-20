namespace VisionApp.Infrastructure.Inspection.Models;

public sealed record YoloXOutput(
	bool Passed,
	double Confidence,
	int ImageWidth,
	int ImageHeight,
	IReadOnlyDictionary<string, int> ClassCounts,
	IReadOnlyList<YoloXDetection> Detections
);
