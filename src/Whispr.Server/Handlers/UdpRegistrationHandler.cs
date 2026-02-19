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

        var user = ctx.State.User!;
        var clientId = udpRegistry.AssignClientId(user.Id);
        ctx.State.ClientId = clientId;

        var response = ControlProtocol.Serialize(MessageTypes.RegisterUdpResponse, new RegisterUdpResponsePayload { ClientId = clientId });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);

        ServerLog.Info($"UDP registered: {user.Username} (clientId={clientId})");

        if (ctx.State.RoomId is { } roomId)
        {
            var msg = ControlProtocol.Serialize(MessageTypes.MemberUdpRegistered, new MemberPayload
            {
                UserId = user.Id,
                Username = user.Username,
                ClientId = clientId
            });
            await ctx.SendToChannelAsync(roomId, msg, user.Id, ctx.CancellationToken);
        }
    }
}
