using System.Collections.Concurrent;
using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Server;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

/// <summary>
/// Routes control messages to focused handlers.
/// Owns user control streams and disconnect cleanup.
/// </summary>
public sealed class ControlMessageRouter(
    IAuthService auth,
    IChannelService channels,
    IMessageService messages,
    UdpEndpointRegistry udpRegistry)
{
    private readonly IReadOnlyDictionary<string, IControlMessageHandler> _handlers = BuildHandlerMap(
        new LoginHandler(auth, channels, udpRegistry),
        new ChannelHandler(auth, channels, udpRegistry),
        new UdpRegistrationHandler(auth, udpRegistry),
        new PermissionHandler(auth),
        new PingHandler(),
        new MessageHandler(auth, messages)
    );
    private readonly ConcurrentDictionary<Guid, (Stream Stream, SessionState State)> _userControlStreams = new();

    private static IReadOnlyDictionary<string, IControlMessageHandler> BuildHandlerMap(params IControlMessageHandler[] handlers)
    {
        var map = new Dictionary<string, IControlMessageHandler>();
        foreach (var h in handlers)
        {
            foreach (var t in h.HandledMessageTypes)
                map[t] = h;
        }
        return map;
    }

    public void RegisterControlStream(Guid userId, Stream stream, SessionState state)
    {
        _userControlStreams[userId] = (stream, state);
    }

    public void UnregisterControlStream(Guid userId)
    {
        _userControlStreams.TryRemove(userId, out _);
    }

    public void OnClientDisconnected(SessionState state)
    {
        if (state.User is null) return;

        if (!string.IsNullOrEmpty(state.Token))
            auth.RevokeToken(state.Token);

        var result = channels.LeaveChannel(state.User.Id);
        if (result is not null)
        {
            var (channelId, _) = result.Value;
            var clientId = state.ClientId ?? 0;
            if (state.ClientId.HasValue)
                udpRegistry.UnregisterByClientId(state.ClientId.Value);

            var memberLeft = ControlProtocol.Serialize(MessageTypes.MemberLeft, new MemberPayload
            {
                UserId = state.User.Id,
                Username = state.User.Username,
                ClientId = clientId
            });
            _ = SendToChannelAsync(channelId, memberLeft);
            ServerLog.Info($"Client disconnected, removed from room: {state.User.Username}");
        }

        UnregisterControlStream(state.User.Id);
    }

    public async Task SendToUserAsync(Guid userId, byte[] message, CancellationToken ct = default)
    {
        if (!_userControlStreams.TryGetValue(userId, out var entry))
            return;

        try
        {
            await entry.Stream.WriteAsync(message, ct);
        }
        catch
        {
            _userControlStreams.TryRemove(userId, out _);
        }
    }

    /// <summary>
    /// Sends a message to all members of a channel, optionally excluding one user.
    /// </summary>
    public async Task SendToChannelAsync(Guid channelId, byte[] message, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        var members = channels.GetOtherMembers(channelId, excludeUserId ?? Guid.Empty);
        if (members is null) return;

        foreach (var memberId in members)
            await SendToUserAsync(memberId, message, ct);
    }

    public async Task HandleAsync(ControlMessage message, Stream stream, SessionState state, CancellationToken ct = default)
    {
        var ctx = new ControlHandlerContext
        {
            Stream = stream,
            State = state,
            CancellationToken = ct,
            SendToUserAsync = (userId, msg, c) => SendToUserAsync(userId, msg, c),
            SendToChannelAsync = (chId, msg, excl, c) => SendToChannelAsync(chId, msg, excl, c),
            RegisterControlStream = (userId, s, st) => RegisterControlStream(userId, s, st),
            IsUserConnected = userId => _userControlStreams.ContainsKey(userId)
        };

        if (!state.TryConsumeRateLimit())
        {
            await ctx.SendErrorAsync("rate_limited", "Too many requests. Please slow down.");
            return;
        }

        if (_handlers.TryGetValue(message.Type, out var handler))
            await handler.HandleAsync(message, ctx);
        else
            await ctx.SendErrorAsync("invalid_message", $"Unknown message type: {message.Type}");
    }
}
