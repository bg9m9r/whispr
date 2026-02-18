using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Server;

namespace Whispr.Server.Handlers;

/// <summary>
/// Context passed to control message handlers. Provides stream, session state, and send helpers.
/// </summary>
public sealed class ControlHandlerContext
{
    public Stream Stream { get; init; } = null!;
    public SessionState State { get; init; } = null!;
    public CancellationToken CancellationToken { get; init; }
    public required Func<Guid, byte[], CancellationToken, Task> SendToUserAsync { get; init; }
    public required Action<Guid, Stream, SessionState> RegisterControlStream { get; init; }

    public async Task SendErrorAsync(string code, string message)
    {
        var bytes = ControlProtocol.Serialize(MessageTypes.Error, new ErrorPayload { Code = code, Message = message });
        await Stream.WriteAsync(bytes, CancellationToken);
    }
}
