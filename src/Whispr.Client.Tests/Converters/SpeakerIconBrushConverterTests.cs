using System.Globalization;
using Avalonia.Media;
using Whispr.Client.Converters;
using Xunit;

namespace Whispr.Client.Tests.Converters;

public sealed class SpeakerIconBrushConverterTests
{
    private static readonly SpeakerIconBrushConverter Converter = SpeakerIconBrushConverter.Instance;

    [Fact]
    public void Convert_True_ReturnsSpeakingBrush()
    {
        var result = Converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.Parse("#3ba55d"), brush.Color);
    }

    [Fact]
    public void Convert_False_ReturnsSilentBrush()
    {
        var result = Converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.Parse("#72767d"), brush.Color);
    }

    [Fact]
    public void Convert_Null_ReturnsSilentBrush()
    {
        var result = Converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        Assert.IsType<SolidColorBrush>(result);
    }

    [Fact]
    public void Convert_NonBool_ReturnsSilentBrush()
    {
        var result = Converter.Convert("invalid", typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        Assert.IsType<SolidColorBrush>(result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() =>
            Converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
