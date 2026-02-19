using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Server;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

internal sealed class UdpRegistrationHandler(IAuthService auth, UdpEndpointRegistry udpRegistry) : IControlMessageHandler
{
    public IReadOnlyList<string> HandledMessageTypes { get; } = [MessageTypes.RegisterUdp];

    public Task HandleAsync(ControlMessage message, ControlHandlerContext ctx) => HandleRegisterUdpAsync(message, ctx);

    private async Task HandleRegisterUdpAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAuthAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<RegisterUdpPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "RegisterUdp payload required");
            return;
        }
        if (!PayloadValidation.IsValidClientId(payload.ClientId, out var clientError))
        {
            await ctx.SendErrorAsync("invalid_payload", clientError!);
            return;
        }

        var user = ctx.State.User!;
        ctx.State.ClientId = payload.ClientId;
        udpRegistry.RegisterClientId(payload.ClientId, user.Id);
        ServerLog.Info($"UDP registered: {user.Username} (clientId={payload.ClientId})");

        if (ctx.State.RoomId is { } roomId)
        {
            var msg = ControlProtocol.Serialize(MessageTypes.MemberUdpRegistered, new MemberPayload
            {
                UserId = user.Id,
                Username = user.Username,
                ClientId = payload.ClientId
            });
            await ctx.SendToChannelAsync(roomId, msg, user.Id, ctx.CancellationToken);
        }
    }
}
