using System;
using System.Globalization;
using System.Windows.Data;
using PingArmor.Services;

namespace PingArmor.UI.Converters;

/// <summary>Maps a <see cref="LogLevel"/> to the matching semantic status brush.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            LogLevel.Error => "StatusErrorBrush",
            LogLevel.Warn => "StatusWarnBrush",
            LogLevel.Debug => "TextFillColorTertiaryBrush",
            _ => "StatusOkBrush"
        };

        return System.Windows.Application.Current?.TryFindResource(key) as System.Windows.Media.Brush
               ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}