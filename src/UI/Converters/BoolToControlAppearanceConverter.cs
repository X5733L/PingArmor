using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace PingArmor.UI.Converters;

/// <summary>Maps a boolean selection flag to a Fluent button appearance (Primary when selected).</summary>
public sealed class BoolToControlAppearanceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ControlAppearance.Primary : ControlAppearance.Secondary;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}