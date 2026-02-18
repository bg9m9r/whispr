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
public sealed class ControlMessageRouter
{
    private readonly LoginHandler _loginHandler;
    private readonly ChannelHandler _channelHandler;
    private readonly PermissionHandler _permissionHandler;
    private readonly UdpRegistrationHandler _udpHandler;
    private readonly IChannelService _channels;
    private readonly UdpEndpointRegistry _udpRegistry;
    private readonly ConcurrentDictionary<Guid, (Stream Stream, SessionState State)> _userControlStreams = new();

    public ControlMessageRouter(
        IAuthService auth,
        IChannelService channels,
        UdpEndpointRegistry udpRegistry)
    {
        _channels = channels;
        _udpRegistry = udpRegistry;
        _loginHandler = new LoginHandler(auth, channels, udpRegistry);
        _channelHandler = new ChannelHandler(auth, channels, udpRegistry);
        _permissionHandler = new PermissionHandler(auth);
        _udpHandler = new UdpRegistrationHandler(channels, udpRegistry);
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

        var result = _channels.LeaveChannel(state.User.Id);
        if (result is not null)
        {
            var (_, remainingMembers) = result.Value;
            var clientId = state.ClientId ?? 0;
            if (state.ClientId.HasValue)
                _udpRegistry.UnregisterByClientId(state.ClientId.Value);

            var memberLeft = ControlProtocol.Serialize(MessageTypes.MemberLeft, new MemberPayload
            {
                UserId = state.User.Id,
                Username = state.User.Username,
                ClientId = clientId
            });
            foreach (var memberId in remainingMembers)
                _ = SendToUserAsync(memberId, memberLeft);
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

    public async Task HandleAsync(ControlMessage message, Stream stream, SessionState state, CancellationToken ct = default)
    {
        var ctx = new ControlHandlerContext
        {
            Stream = stream,
            State = state,
            CancellationToken = ct,
            SendToUserAsync = (userId, msg, c) => SendToUserAsync(userId, msg, c),
            RegisterControlStream = (userId, s, st) => RegisterControlStream(userId, s, st)
        };

        switch (message.Type)
        {
            case MessageTypes.LoginRequest:
                await _loginHandler.HandleLoginAsync(message, ctx);
                break;
            case MessageTypes.CreateRoom:
            case MessageTypes.CreateChannel:
                await _channelHandler.HandleCreateChannelAsync(message, ctx);
                break;
            case MessageTypes.JoinRoom:
            case MessageTypes.JoinChannel:
                await _channelHandler.HandleJoinChannelAsync(message, ctx);
                break;
            case MessageTypes.LeaveRoom:
                await _channelHandler.HandleLeaveChannelAsync(ctx);
                break;
            case MessageTypes.RegisterUdp:
                await _udpHandler.HandleRegisterUdpAsync(message, ctx);
                break;
            case MessageTypes.RequestRoomList:
                await _channelHandler.HandleRequestRoomListAsync(ctx);
                break;
            case MessageTypes.RequestServerState:
                await _channelHandler.HandleRequestServerStateAsync(ctx);
                break;
            case MessageTypes.Ping:
                await LoginHandler.HandlePingAsync(stream, ct);
                break;
            case MessageTypes.ListPermissions:
                await _permissionHandler.HandleListPermissionsAsync(ctx);
                break;
            case MessageTypes.ListRoles:
                await _permissionHandler.HandleListRolesAsync(ctx);
                break;
            case MessageTypes.GetUserPermissions:
                await _permissionHandler.HandleGetUserPermissionsAsync(message, ctx);
                break;
            case MessageTypes.SetUserPermission:
                await _permissionHandler.HandleSetUserPermissionAsync(message, ctx);
                break;
            case MessageTypes.SetUserRole:
                await _permissionHandler.HandleSetUserRoleAsync(message, ctx);
                break;
            case MessageTypes.GetChannelPermissions:
                await _permissionHandler.HandleGetChannelPermissionsAsync(message, ctx);
                break;
            case MessageTypes.SetChannelRolePermission:
                await _permissionHandler.HandleSetChannelRolePermissionAsync(message, ctx);
                break;
            case MessageTypes.SetChannelUserPermission:
                await _permissionHandler.HandleSetChannelUserPermissionAsync(message, ctx);
                break;
            default:
                await ctx.SendErrorAsync("invalid_message", $"Unknown message type: {message.Type}");
                break;
        }
    }
}
