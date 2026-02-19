using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Server;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

internal sealed class LoginHandler(IAuthService auth, IChannelService channels, UdpEndpointRegistry udpRegistry) : IControlMessageHandler
{
    public IReadOnlyList<string> HandledMessageTypes { get; } = [MessageTypes.LoginRequest];

    public Task HandleAsync(ControlMessage message, ControlHandlerContext ctx) => HandleLoginAsync(message, ctx);

    private async Task HandleLoginAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        var payload = ControlProtocol.DeserializePayload<LoginRequestPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "Login payload required");
            return;
        }
        if (!PayloadValidation.IsValidUsername(payload.Username, out var usernameError))
        {
            await ctx.SendErrorAsync("invalid_payload", usernameError!);
            return;
        }

        var user = auth.ValidateOrRegister(payload.Username, payload.Password);
        if (user is null)
        {
            ServerLog.Warn($"Login failed: invalid credentials for user '{payload.Username}'");
            var bytes = ControlProtocol.Serialize(MessageTypes.LoginResponse, new LoginResponsePayload
            {
                Success = false,
                Error = "Invalid credentials"
            });
            await ctx.Stream.WriteAsync(bytes, ctx.CancellationToken);
            return;
        }

        if (ctx.IsUserConnected(user.Id))
        {
            ServerLog.Info($"Login rejected (already connected): {user.Username}");
            var bytes = ControlProtocol.Serialize(MessageTypes.LoginResponse, new LoginResponsePayload
            {
                Success = false,
                Error = "Already logged in from another session"
            });
            await ctx.Stream.WriteAsync(bytes, ctx.CancellationToken);
            return;
        }

        ServerLog.Info($"Login: {user.Username}");
        var token = auth.IssueSessionToken(user);
        ctx.State.User = user;
        ctx.State.Token = token;
        ctx.State.ControlStream = ctx.Stream;

        ctx.RegisterControlStream(user.Id, ctx.Stream, ctx.State);

        var response = ControlProtocol.Serialize(MessageTypes.LoginResponse, new LoginResponsePayload
        {
            Success = true,
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role.ToString().ToLowerInvariant(),
            IsAdmin = auth.IsAdmin(user.Id)
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);

        var joinResult = channels.JoinDefaultChannel(user.Id);
        if (joinResult is not null)
        {
            var (channel, keyMaterial) = joinResult.Value;
            ctx.State.RoomId = channel.Id;
            var members = channel.MemberIds.Select(id => new MemberInfo
            {
                UserId = id,
                Username = auth.GetUsername(id) ?? id.ToString(),
                ClientId = udpRegistry.GetClientId(id) ?? 0,
                IsAdmin = auth.IsAdmin(id)
            }).ToList();
            var roomJoined = ControlProtocol.Serialize(MessageTypes.RoomJoined, new RoomJoinedPayload
            {
                RoomId = channel.Id,
                RoomName = channel.Name,
                MemberIds = channel.MemberIds,
                Members = members,
                KeyMaterial = keyMaterial
            });
            await ctx.Stream.WriteAsync(roomJoined, ctx.CancellationToken);

            var channelInfos = channels.ListChannels().Select(c =>
            {
                var m = c.MemberIds.Select(id => new MemberInfo
                {
                    UserId = id,
                    Username = auth.GetUsername(id) ?? id.ToString(),
                    ClientId = udpRegistry.GetClientId(id) ?? 0,
                    IsAdmin = auth.IsAdmin(id)
                }).ToList();
                return new ChannelInfo { Id = c.Id, Name = c.Name, MemberIds = c.MemberIds, Members = m };
            }).ToList();
            var serverState = ControlProtocol.Serialize(MessageTypes.ServerState, new ServerStatePayload
            {
                Channels = channelInfos,
                CanCreateChannel = channels.CanCreateMoreChannels
            });
            await ctx.Stream.WriteAsync(serverState, ctx.CancellationToken);

            var memberJoined = ControlProtocol.Serialize(MessageTypes.MemberJoined, new MemberPayload
            {
                UserId = user.Id,
                Username = user.Username,
                ClientId = 0
            });
            await ctx.SendToChannelAsync(channel.Id, memberJoined, user.Id, ctx.CancellationToken);
        }
    }
}
