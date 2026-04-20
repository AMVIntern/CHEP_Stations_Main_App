namespace VisionApp.Infrastructure.Inspection.Steps.Filters;

/// <summary>
/// Defines how much of the image to keep around the vertical centerline.
/// Rules are expressed as fractions of half-height (0..1).
///
/// topKeepHalfPct = 1.0  => keep full top half (yMin = 0)
/// topKeepHalfPct = 0.54 => keep 54% of the top half closest to center
///
/// bottomKeepHalfPct = 1.0 => keep full bottom half (yMax = imageHeight)
/// bottomKeepHalfPct = 0.60 => keep 60% of the bottom half closest to center
/// </summary>
public sealed record VerticalBandRule(
	double TopKeepHalfPct,
	double BottomKeepHalfPct)
{
	public static VerticalBandRule KeepAll() => new(1.0, 1.0);
}

/// <summary>
/// Config-binding DTO for <see cref="VerticalBandRule"/>.
/// Positional records cannot be directly bound from IConfiguration,
/// so this mutable class acts as the appsettings counterpart.
/// </summary>
public sealed class VerticalBandRuleOptions
{
	public double TopKeepHalfPct { get; set; } = 1.0;
	public double BottomKeepHalfPct { get; set; } = 1.0;

	public VerticalBandRule ToRule() => new(TopKeepHalfPct, BottomKeepHalfPct);
}
