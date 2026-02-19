using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Server.Handlers;

internal sealed class PingHandler : IControlMessageHandler
{
    public IReadOnlyList<string> HandledMessageTypes { get; } = [MessageTypes.Ping];

    public Task HandleAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        var pong = ControlProtocol.Serialize(MessageTypes.Pong, new { });
        return ctx.Stream.WriteAsync(pong, ctx.CancellationToken).AsTask();
    }
}
