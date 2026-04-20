namespace VisionApp.Core.Domain;

/// <summary>
/// UI-safe overlay primitives (no HALCON/OpenCV objects).
/// Coordinates are in image pixel space (X,Y,Width,Height).
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height);

public sealed record OverlayBox(
	string Label,
	double Confidence,
	RectD Rect);

public sealed record InspectionVisuals(
	IReadOnlyDictionary<string, IReadOnlyList<OverlayBox>> BoxesByStep)
{
	public static readonly InspectionVisuals Empty =
		new(new Dictionary<string, IReadOnlyList<OverlayBox>>());

	public IReadOnlyList<OverlayBox> AllBoxes =>
		BoxesByStep.SelectMany(kvp => kvp.Value).ToList();
}
