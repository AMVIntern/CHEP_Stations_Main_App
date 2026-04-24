using System.Globalization;
using System.Windows.Data;

namespace VisionApp.Wpf.Converters;

/// <summary>Formats threshold as F3 for display; parses typed values on write-back.</summary>
public sealed class ThresholdTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return d.ToString("F3", CultureInfo.InvariantCulture);
        return "0.000";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return Binding.DoNothing;

        if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return Binding.DoNothing;

        return Math.Clamp(d, 0.0, 1.0);
    }
}
