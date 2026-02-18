using System.Text;
using Whispr.Core.Protocol;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class ControlProtocolTests
{
    [Fact]
    public void Serialize_AndDeserialize_RoundTrips()
    {
        var message = new ControlMessage
        {
            Type = "test",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { foo = "bar" })
        };
        var bytes = ControlProtocol.Serialize(message);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length >= 4);

        var length = bytes.Length - 4;
        var payload = bytes.AsSpan(4);
        var deserialized = ControlProtocol.Deserialize(payload);
        Assert.Equal("test", deserialized.Type);
        Assert.NotNull(deserialized.Payload);
    }

    [Fact]
    public void Serialize_WithTypedPayload_CreatesValidMessage()
    {
        var bytes = ControlProtocol.Serialize("login_response", new { success = true, token = "abc" });
        Assert.NotNull(bytes);
        Assert.True(bytes.Length >= 4);

        var length = BitConverter.ToInt32(bytes.AsSpan(0, 4));
        Assert.Equal(bytes.Length - 4, length);

        var json = Encoding.UTF8.GetString(bytes.AsSpan(4));
        Assert.Contains("login_response", json);
        Assert.Contains("success", json);
        Assert.Contains("abc", json);
    }

    [Fact]
    public void DeserializePayload_ExtractsTypedPayload()
    {
        var message = new ControlMessage
        {
            Type = "login_response",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { success = true, token = "xyz", userId = "00000000-0000-0000-0000-000000000000" })
        };
        var payload = ControlProtocol.DeserializePayload<LoginResponsePayload>(message);
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.Equal("xyz", payload.Token);
    }

    [Fact]
    public async Task TryReadAsync_ReadsLengthPrefixedMessage()
    {
        var message = new ControlMessage { Type = "pong", Payload = null };
        var bytes = ControlProtocol.Serialize(message);
        var stream = new MemoryStream(bytes);

        var read = await ControlProtocol.TryReadAsync(stream);
        Assert.NotNull(read);
        Assert.Equal("pong", read.Type);
    }

    [Fact]
    public async Task TryReadAsync_PartialData_ReturnsNull()
    {
        var stream = new MemoryStream(new byte[] { 0, 0, 0 });
        var read = await ControlProtocol.TryReadAsync(stream);
        Assert.Null(read);
    }
}
