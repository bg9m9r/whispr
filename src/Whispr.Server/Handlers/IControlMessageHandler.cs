using Whispr.Core.Protocol;

namespace Whispr.Server.Handlers;

/// <summary>
/// Handles one or more control message types. Register in ControlMessageRouter to avoid editing the switch when adding new types.
/// </summary>
public interface IControlMessageHandler
{
    /// <summary>
    /// Message type(s) this handler handles. A type may map to only one handler.
    /// </summary>
    IReadOnlyList<string> HandledMessageTypes { get; }

    Task HandleAsync(ControlMessage message, ControlHandlerContext ctx);
}
