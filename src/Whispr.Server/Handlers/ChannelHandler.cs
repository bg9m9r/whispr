using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Server;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

internal sealed class ChannelHandler
{
    private readonly IAuthService _auth;
    private readonly IChannelService _channels;
    private readonly UdpEndpointRegistry _udpRegistry;

    public ChannelHandler(IAuthService auth, IChannelService channels, UdpEndpointRegistry udpRegistry)
    {
        _auth = auth;
        _channels = channels;
        _udpRegistry = udpRegistry;
    }

    public async Task HandleCreateChannelAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await RequireAuthAsync(ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<CreateRoomPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "CreateChannel payload required");
            return;
        }

        var user = ctx.State.User!;
        var channel = _channels.CreateChannel(payload.Name, user.Id);
        if (channel is null)
        {
            await ctx.SendErrorAsync("create_failed", "Maximum channels (10) reached");
            return;
        }

        var leaveResult = _channels.LeaveChannel(user.Id);
        if (leaveResult is not null)
        {
            var (_, remainingMembers) = leaveResult.Value;
            var clientId = ctx.State.ClientId ?? 0;
            ctx.State.RoomId = null;
            if (ctx.State.ClientId.HasValue)
            {
                _udpRegistry.UnregisterByClientId(ctx.State.ClientId.Value);
                ctx.State.ClientId = null;
            }
            var memberLeft = ControlProtocol.Serialize(MessageTypes.MemberLeft, new MemberPayload
            {
                UserId = user.Id,
                Username = user.Username,
                ClientId = clientId
            });
            foreach (var memberId in remainingMembers)
                await ctx.SendToUserAsync(memberId, memberLeft, ctx.CancellationToken);
        }

        var result = _channels.JoinChannel(channel.Id, user.Id);
        if (result is null)
        {
            await ctx.SendErrorAsync("create_failed", "Could not join new channel");
            return;
        }

        var (joinedChannel, keyMaterial) = result.Value;
        ServerLog.Info($"Channel created: {joinedChannel.Name} by {user.Username}");
        ctx.State.RoomId = joinedChannel.Id;

        var members = joinedChannel.MemberIds.Select(id => new MemberInfo
        {
            UserId = id,
            Username = _auth.GetUsername(id) ?? id.ToString(),
            ClientId = _udpRegistry.GetClientId(id) ?? 0,
            IsAdmin = _auth.IsAdmin(id)
        }).ToList();
        var response = ControlProtocol.Serialize(MessageTypes.RoomJoined, new RoomJoinedPayload
        {
            RoomId = joinedChannel.Id,
            RoomName = joinedChannel.Name,
            MemberIds = joinedChannel.MemberIds,
            Members = members,
            KeyMaterial = keyMaterial
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    public async Task HandleJoinChannelAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await RequireAuthAsync(ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<JoinRoomPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "JoinRoom payload required");
            return;
        }

        var user = ctx.State.User!;
        var leaveResult = _channels.LeaveChannel(user.Id);
        if (leaveResult is not null)
        {
            var (oldRoomId, remainingMembers) = leaveResult.Value;
            var clientId = ctx.State.ClientId ?? 0;
            ctx.State.RoomId = null;
            if (ctx.State.ClientId.HasValue)
            {
                _udpRegistry.UnregisterByClientId(ctx.State.ClientId.Value);
                ctx.State.ClientId = null;
            }
            ctx.State.UdpEndpoint = null;
            var memberLeft = ControlProtocol.Serialize(MessageTypes.MemberLeft, new MemberPayload
            {
                UserId = user.Id,
                Username = user.Username,
                ClientId = clientId
            });
            foreach (var memberId in remainingMembers)
                await ctx.SendToUserAsync(memberId, memberLeft, ctx.CancellationToken);
            ServerLog.Info($"Join room: {user.Username} left previous room {oldRoomId}");
        }

        if (!_auth.CanAccessChannel(user.Id, payload.RoomId))
        {
            ServerLog.Info($"Join room denied: {user.Username} (room {payload.RoomId}) - no channel access");
            await ctx.SendErrorAsync("access_denied", "You do not have permission to access this channel");
            return;
        }

        var result = _channels.JoinChannel(payload.RoomId, user.Id);
        if (result is null)
        {
            ServerLog.Info($"Join room failed: {user.Username} (room {payload.RoomId})");
            await ctx.SendErrorAsync("join_failed", "Room not found");
            return;
        }

        ctx.State.RoomId = payload.RoomId;
        var (room, keyMaterial) = result.Value;
        ServerLog.Info($"Join room: {user.Username} joined {room.Name} ({room.MemberIds.Count} members)");

        var members = room.MemberIds.Select(id => new MemberInfo
        {
            UserId = id,
            Username = _auth.GetUsername(id) ?? id.ToString(),
            ClientId = _udpRegistry.GetClientId(id) ?? 0,
            IsAdmin = _auth.IsAdmin(id)
        }).ToList();
        var response = ControlProtocol.Serialize(MessageTypes.RoomJoined, new RoomJoinedPayload
        {
            RoomId = room.Id,
            RoomName = room.Name,
            MemberIds = room.MemberIds,
            Members = members,
            KeyMaterial = keyMaterial
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);

        var memberJoined = ControlProtocol.Serialize(MessageTypes.MemberJoined, new MemberPayload
        {
            UserId = user.Id,
            Username = user.Username,
            ClientId = 0
        });
        foreach (var memberId in room.MemberIds)
        {
            if (memberId == user.Id) continue;
            await ctx.SendToUserAsync(memberId, memberJoined, ctx.CancellationToken);
        }
    }

    public async Task HandleLeaveChannelAsync(ControlHandlerContext ctx)
    {
        if (!await RequireAuthAsync(ctx))
            return;

        var user = ctx.State.User!;
        var result = _channels.LeaveChannel(user.Id);
        if (result is null)
        {
            await ctx.SendErrorAsync("not_in_room", "You are not in a room");
            return;
        }

        ServerLog.Info($"Leave room: {user.Username}");
        var clientId = ctx.State.ClientId ?? 0;
        ctx.State.RoomId = null;
        if (ctx.State.ClientId.HasValue)
        {
            _udpRegistry.UnregisterByClientId(ctx.State.ClientId.Value);
            ctx.State.ClientId = null;
        }
        ctx.State.UdpEndpoint = null;

        var (roomId, remainingMembers) = result.Value;

        var leaveResponse = ControlProtocol.Serialize(MessageTypes.RoomLeft, new { RoomId = roomId });
        await ctx.Stream.WriteAsync(leaveResponse, ctx.CancellationToken);

        var memberLeft = ControlProtocol.Serialize(MessageTypes.MemberLeft, new MemberPayload
        {
            UserId = user.Id,
            Username = user.Username,
            ClientId = clientId
        });
        foreach (var memberId in remainingMembers)
        {
            await ctx.SendToUserAsync(memberId, memberLeft, ctx.CancellationToken);
        }
    }

    public async Task HandleRequestRoomListAsync(ControlHandlerContext ctx)
    {
        if (!await RequireAuthAsync(ctx))
            return;

        var channels = _channels.ListChannels();
        var rooms = channels.Select(c => new RoomInfo { Id = c.Id, Name = c.Name, MemberCount = c.MemberIds.Count }).ToList();
        var response = ControlProtocol.Serialize(MessageTypes.RoomList, new RoomListPayload { Rooms = rooms });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    public async Task HandleRequestServerStateAsync(ControlHandlerContext ctx)
    {
        if (!await RequireAuthAsync(ctx))
            return;

        var user = ctx.State.User!;
        var channels = _channels.ListChannels();
        var accessibleChannels = channels.Where(c => _auth.CanAccessChannel(user.Id, c.Id)).ToList();
        var channelInfos = accessibleChannels.Select(c =>
        {
            var members = c.MemberIds.Select(id => new MemberInfo
            {
                UserId = id,
                Username = _auth.GetUsername(id) ?? id.ToString(),
                ClientId = _udpRegistry.GetClientId(id) ?? 0,
                IsAdmin = _auth.IsAdmin(id)
            }).ToList();
            return new ChannelInfo { Id = c.Id, Name = c.Name, MemberIds = c.MemberIds, Members = members };
        }).ToList();
        var response = ControlProtocol.Serialize(MessageTypes.ServerState, new ServerStatePayload
        {
            Channels = channelInfos,
            CanCreateChannel = _channels.CanCreateMoreChannels
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    private async Task<bool> RequireAuthAsync(ControlHandlerContext ctx)
    {
        if (ctx.State.User is null || ctx.State.Token is null)
        {
            await ctx.SendErrorAsync("unauthorized", "Login required");
            return false;
        }
        if (_auth.ValidateToken(ctx.State.Token) is null)
        {
            await ctx.SendErrorAsync("invalid_token", "Session expired");
            return false;
        }
        return true;
    }
}
