using CommunityToolkit.Mvvm.ComponentModel;

namespace VisionApp.Wpf.ViewModels;

/// <summary>
/// Represents a single defect class row in the Settings confidence threshold editor.
/// </summary>
public sealed class DefectThresholdItem : ObservableObject
{
    /// <summary>The raw config key, e.g. "Raised_Nail".</summary>
    public string ClassKey { get; }

    /// <summary>Human-readable label, e.g. "Raised Nail".</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Whether this class belongs to the secondary model.
    /// Used by SettingsViewModel when writing back to JSON.
    /// </summary>
    public bool IsSecondaryModel { get; }

    private double _threshold;

    /// <summary>Confidence threshold (0.0 – 1.0). Two-way bound to the slider.</summary>
    public double Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, Math.Clamp(value, 0.0, 1.0));
    }

    public DefectThresholdItem(string classKey, string displayName, double threshold, bool isSecondaryModel)
    {
        ClassKey = classKey;
        DisplayName = displayName;
        _threshold = Math.Clamp(threshold, 0.0, 1.0);
        IsSecondaryModel = isSecondaryModel;
    }
}
