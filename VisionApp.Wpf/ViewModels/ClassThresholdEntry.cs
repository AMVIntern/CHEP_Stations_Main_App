using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.RegularExpressions;

namespace VisionApp.Wpf.ViewModels;

/// <summary>
/// Represents a confidence threshold setting for a single defect class.
/// Handles display name formatting (CamelCase + underscore conversion).
/// </summary>
public sealed partial class ClassThresholdEntry : ObservableObject
{
	/// <summary>
	/// The raw class label as it appears in config (e.g., "EndPlateDamage", "Raised_Nail").
	/// </summary>
	public string ClassName { get; }

	/// <summary>
	/// Formatted display name suitable for UI (e.g., "End Plate Damage", "Raised Nail").
	/// </summary>
	public string DisplayName { get; }

	/// <summary>
	/// Editable confidence threshold (0.0–1.0).
	/// </summary>
	[ObservableProperty] private double _threshold;

	public ClassThresholdEntry(string className, double threshold)
	{
		ClassName = className;
		_threshold = threshold;
		DisplayName = FormatDisplayName(className);
	}

	/// <summary>
	/// Converts a class name to a human-readable display name.
	/// Handles both CamelCase (EndPlateDamage → "End Plate Damage")
	/// and underscore-separated names (Raised_Nail → "Raised Nail").
	/// </summary>
	private static string FormatDisplayName(string className)
	{
		if (string.IsNullOrWhiteSpace(className))
			return className;

		// Replace underscores with spaces
		var withSpaces = className.Replace('_', ' ');

		// Insert space between lowercase and uppercase (CamelCase splitting)
		var withCamelSpaces = Regex.Replace(withSpaces, "(?<=[a-z])(?=[A-Z])", " ");

		// Title case each word
		var titleCased = System.Globalization.CultureInfo.CurrentCulture
			.TextInfo.ToTitleCase(withCamelSpaces.ToLower());

		return titleCased;
	}
}
