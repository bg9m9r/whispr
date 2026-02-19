using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Whispr.Client.Converters;

/// <summary>
/// Converts channel type string ("voice" or "text") to bool. Parameter "text" or "voice" selects which returns true.
/// </summary>
public sealed class ChannelTypeToBoolConverter : IValueConverter
{
    public static readonly ChannelTypeToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString() ?? "";
        var match = parameter?.ToString() ?? "voice";
        return string.Equals(s, match, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
