using Whispr.Server.Handlers;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class PayloadValidationTests
{
    [Fact]
    public void SanitizeMessageContent_WithEmoji_PreservesContent()
    {
        var content = "Hello 😀";
        var result = PayloadValidation.SanitizeMessageContent(content);
        Assert.Equal("Hello 😀", result);
    }

    [Fact]
    public void SanitizeMessageContent_WithMultipleEmoji_PreservesContent()
    {
        var content = "👍 🎉 ✅";
        var result = PayloadValidation.SanitizeMessageContent(content);
        Assert.Equal("👍 🎉 ✅", result);
    }
}
