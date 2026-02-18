using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Server;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

internal sealed class UdpRegistrationHandler(IChannelService channels, UdpEndpointRegistry udpRegistry)
{
    public async Task HandleRegisterUdpAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (ctx.State.User is null || ctx.State.Token is null)
            return;

        var payload = ControlProtocol.DeserializePayload<RegisterUdpPayload>(message);
        if (payload is null)
            return;

        ctx.State.ClientId = payload.ClientId;
        udpRegistry.RegisterClientId(payload.ClientId, ctx.State.User.Id);
        ServerLog.Info($"UDP registered: {ctx.State.User.Username} (clientId={payload.ClientId})");

        if (ctx.State.RoomId is { } roomId)
        {
            var otherMembers = channels.GetOtherMembers(roomId, ctx.State.User.Id);
            if (otherMembers is not null)
            {
                var msg = ControlProtocol.Serialize(MessageTypes.MemberUdpRegistered, new MemberPayload
                {
                    UserId = ctx.State.User.Id,
                    Username = ctx.State.User.Username,
                    ClientId = payload.ClientId
                });
                foreach (var memberId in otherMembers)
                    await ctx.SendToUserAsync(memberId, msg, ctx.CancellationToken);
            }
        }
    }
}
