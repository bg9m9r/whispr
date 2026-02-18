using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Whispr.Client.Converters;

/// <summary>
/// Returns highlighted (green) brush when true (speaking), muted (gray) brush when false.
/// </summary>
public sealed class SpeakerIconBrushConverter : IValueConverter
{
    public static readonly SpeakerIconBrushConverter Instance = new();

    private static readonly SolidColorBrush SpeakingBrush = new(Color.Parse("#3ba55d"));
    private static readonly SolidColorBrush SilentBrush = new(Color.Parse("#72767d"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? SpeakingBrush : SilentBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
