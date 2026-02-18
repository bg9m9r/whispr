using System.Globalization;
using Whispr.Client.Converters;
using Xunit;

namespace Whispr.Client.Tests.Converters;

public sealed class InverseBoolConverterTests
{
    private static readonly InverseBoolConverter Converter = InverseBoolConverter.Instance;

    [Fact]
    public void Convert_True_ReturnsFalse()
    {
        var result = Converter.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_False_ReturnsTrue()
    {
        var result = Converter.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_NonBool_ReturnsValueUnchanged()
    {
        var result = Converter.Convert("hello", typeof(object), null, CultureInfo.InvariantCulture);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Convert_Null_ReturnsNull()
    {
        var result = Converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    [Fact]
    public void ConvertBack_True_ReturnsFalse()
    {
        var result = Converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_False_ReturnsTrue()
    {
        var result = Converter.ConvertBack(false, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.Equal(true, result);
    }
}
