using System.Globalization;
using Avalonia.Media;
using Whispr.Client.Converters;
using Xunit;

namespace Whispr.Client.Tests.Converters;

public sealed class SpeakingGlowEffectConverterTests
{
    private static readonly SpeakingGlowEffectConverter Converter = SpeakingGlowEffectConverter.Instance;

    [Fact]
    public void Convert_True_ReturnsGlowEffect()
    {
        var result = Converter.Convert(true, typeof(object), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        var effect = Assert.IsType<DropShadowEffect>(result);
        Assert.Equal(6, effect.BlurRadius);
        Assert.Equal(Color.Parse("#3ba55d"), effect.Color);
        Assert.Equal(0.9, effect.Opacity);
    }

    [Fact]
    public void Convert_False_ReturnsNull()
    {
        var result = Converter.Convert(false, typeof(object), null, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_Null_ReturnsNull()
    {
        var result = Converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_NonBool_ReturnsNull()
    {
        var result = Converter.Convert("invalid", typeof(object), null, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() =>
            Converter.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
