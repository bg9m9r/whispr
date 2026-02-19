using System.Text.Json;
using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Handlers;
using Whispr.Server.Repositories;
using Whispr.Server.Server;
using Whispr.Server.Services;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class ControlMessageRouterTests
{
    private static ControlMessageRouter CreateRouter()
    {
        var auth = new AuthService(new InMemoryUserRepository(), new InMemoryPermissionRepository());
        var channels = new ChannelManager(new InMemoryChannelRepository());
        var messages = new MessageService(new InMemoryMessageRepository(), auth);
        var udpRegistry = new UdpEndpointRegistry();
        return new ControlMessageRouter(auth, channels, messages, udpRegistry);
    }

    private static ControlMessage CreateMessage(string type, object? payload = null)
    {
        var payloadElement = payload is null
            ? (JsonElement?)null
            : JsonSerializer.SerializeToElement(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new ControlMessage { Type = type, Payload = payloadElement };
    }

    private static async Task<ControlMessage?> ReadResponseAsync(MemoryStream stream)
    {
        stream.Position = 0;
        return await ControlProtocol.TryReadAsync(stream);
    }

    [Fact]
    public async Task HandleAsync_Ping_ReturnsPong()
    {
        var router = CreateRouter();
        var stream = new MemoryStream();
        var state = new SessionState();

        await router.HandleAsync(CreateMessage(MessageTypes.Ping), stream, state, CancellationToken.None);

        var response = await ReadResponseAsync(stream);
        Assert.NotNull(response);
        Assert.Equal(MessageTypes.Pong, response.Type);
    }

    [Fact]
    public async Task HandleAsync_LoginRequest_ValidCredentials_ReturnsSuccess()
    {
        var router = CreateRouter();
        var stream = new MemoryStream();
        var state = new SessionState();

        await router.HandleAsync(
            CreateMessage(MessageTypes.LoginRequest, new { username = "admin", password = "admin" }),
            stream, state, CancellationToken.None);

        var response = await ReadResponseAsync(stream);
        Assert.NotNull(response);
        Assert.Equal(MessageTypes.LoginResponse, response.Type);
        var payload = ControlProtocol.DeserializePayload<LoginResponsePayload>(response);
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Token);
        Assert.Equal("admin", payload.Username);
    }

    [Fact]
    public async Task HandleAsync_LoginRequest_InvalidCredentials_ReturnsError()
    {
        var router = CreateRouter();
        var stream = new MemoryStream();
        var state = new SessionState();

        await router.HandleAsync(
            CreateMessage(MessageTypes.LoginRequest, new { username = "admin", password = "wrong" }),
            stream, state, CancellationToken.None);

        var response = await ReadResponseAsync(stream);
        Assert.NotNull(response);
        Assert.Equal(MessageTypes.LoginResponse, response.Type);
        var payload = ControlProtocol.DeserializePayload<LoginResponsePayload>(response);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("Invalid credentials", payload.Error);
    }

    [Fact]
    public async Task HandleAsync_UnknownMessageType_SendsError()
    {
        var router = CreateRouter();
        var stream = new MemoryStream();
        var state = new SessionState();

        await router.HandleAsync(CreateMessage("unknown_type"), stream, state, CancellationToken.None);

        var response = await ReadResponseAsync(stream);
        Assert.NotNull(response);
        Assert.Equal(MessageTypes.Error, response.Type);
        var payload = ControlProtocol.DeserializePayload<ErrorPayload>(response);
        Assert.NotNull(payload);
        Assert.Equal("invalid_message", payload.Code);
    }

    [Fact]
    public async Task HandleAsync_RequestRoomList_WithoutAuth_SendsUnauthorized()
    {
        var router = CreateRouter();
        var stream = new MemoryStream();
        var state = new SessionState();

        await router.HandleAsync(CreateMessage(MessageTypes.RequestRoomList), stream, state, CancellationToken.None);

        var response = await ReadResponseAsync(stream);
        Assert.NotNull(response);
        Assert.Equal(MessageTypes.Error, response.Type);
        var payload = ControlProtocol.DeserializePayload<ErrorPayload>(response);
        Assert.NotNull(payload);
        Assert.Equal("unauthorized", payload.Code);
    }

    [Fact]
    public async Task HandleAsync_RequestRoomList_WithAuth_ReturnsRoomList()
    {
        var router = CreateRouter();
        var loginStream = new MemoryStream();
        var state = new SessionState();

        await router.HandleAsync(
            CreateMessage(MessageTypes.LoginRequest, new { username = "admin", password = "admin" }),
            loginStream, state, CancellationToken.None);

        Assert.NotNull(state.User);
        Assert.NotNull(state.Token);

        var roomListStream = new MemoryStream();
        await router.HandleAsync(CreateMessage(MessageTypes.RequestRoomList), roomListStream, state, CancellationToken.None);

        var response = await ReadResponseAsync(roomListStream);
        Assert.NotNull(response);
        Assert.Equal(MessageTypes.RoomList, response.Type);
    }

    [Fact]
    public void OnClientDisconnected_RemovesUserFromChannel()
    {
        var auth = new AuthService(new InMemoryUserRepository(), new InMemoryPermissionRepository());
        var channels = new ChannelManager(new InMemoryChannelRepository());
        var messages = new MessageService(new InMemoryMessageRepository(), auth);
        var udpRegistry = new UdpEndpointRegistry();
        var router = new ControlMessageRouter(auth, channels, messages, udpRegistry);

        var user = auth.ValidateCredentials("admin", "admin")!;
        var joinResult = channels.JoinDefaultChannel(user.Id);
        Assert.NotNull(joinResult);

        var state = new SessionState { User = user };
        router.OnClientDisconnected(state);

        Assert.Null(channels.GetUserChannel(user.Id));
    }

    [Fact]
    public async Task SendToUserAsync_UserNotRegistered_NoThrow()
    {
        var router = CreateRouter();
        var msg = ControlProtocol.Serialize(MessageTypes.Pong, new { });
        await router.SendToUserAsync(Guid.NewGuid(), msg);
    }
}
