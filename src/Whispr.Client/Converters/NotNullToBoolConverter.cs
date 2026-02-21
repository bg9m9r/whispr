using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Whispr.Client.Converters;

/// <summary>
/// Returns true when value is not null, false when null. Used e.g. for showing "(edited)" when UpdatedAt has a value.
/// </summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    public static readonly NotNullToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
