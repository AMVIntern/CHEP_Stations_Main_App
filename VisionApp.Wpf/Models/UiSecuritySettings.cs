namespace VisionApp.Wpf.Models;

/// <summary>
/// Optional UI gate for sensitive nav actions (Settings, Exit).
/// If <see cref="Password"/> is null or whitespace, no prompt is shown.
/// </summary>
public sealed class UiSecuritySettings
{
	public const string SectionName = "UiSecurity";

	/// <summary>Plaintext password from configuration (operator station).</summary>
	public string Password { get; set; } = string.Empty;
}
