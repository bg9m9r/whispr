using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Whispr.Client.Converters;

/// <summary>
/// Returns a green glow effect when speaking, null otherwise.
/// </summary>
public sealed class SpeakingGlowEffectConverter : IValueConverter
{
    public static readonly SpeakingGlowEffectConverter Instance = new();

    private static readonly DropShadowEffect GlowEffect = new()
    {
        BlurRadius = 6,
        Color = Color.Parse("#3ba55d"),
        Opacity = 0.9,
        OffsetX = 0,
        OffsetY = 0
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? GlowEffect : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
