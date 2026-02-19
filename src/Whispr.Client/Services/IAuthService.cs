using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Authentication and room operations via the control channel.
/// </summary>
public interface IAuthService
{
    bool IsLoggedIn { get; }
    User? User { get; }
    string? Token { get; }
    bool IsAdmin { get; }

    Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<(ChannelJoinedResult ChannelJoined, ServerStatePayload ServerState)?> ReadInitialServerStateAsync(CancellationToken ct = default);
    Task SendLeaveRoomAsync(CancellationToken ct = default);
    Task RegisterUdpAsync(uint clientId, CancellationToken ct = default);
}
