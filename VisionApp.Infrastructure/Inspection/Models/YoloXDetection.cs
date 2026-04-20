using OpenCvSharp;

namespace VisionApp.Infrastructure.Inspection.Models;

public sealed record YoloXDetection(
	string Label,
	Rect Rect,
	double Probability
);
