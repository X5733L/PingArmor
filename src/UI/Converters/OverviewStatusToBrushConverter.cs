using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PingArmor.UI.ViewModels;

namespace PingArmor.UI.Converters;

/// <summary>Maps an <see cref="OverviewStatus"/> to the matching semantic status brush.</summary>
public sealed class OverviewStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            OverviewStatus.Protected => "StatusOkBrush",
            OverviewStatus.Adjusting => "StatusWarnBrush",
            OverviewStatus.Paused => "StatusPausedBrush",
            _ => "TextFillColorTertiaryBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as System.Windows.Media.Brush
               ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}