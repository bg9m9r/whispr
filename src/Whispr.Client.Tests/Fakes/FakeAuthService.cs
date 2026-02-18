using Whispr.Client.Services;
using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// Configurable fake IAuthService for unit testing.
/// </summary>
public sealed class FakeAuthService : IAuthService
{
    public bool IsLoggedIn { get; set; }
    public User? User { get; set; }
    public string? Token { get; set; }
    public bool IsAdmin { get; set; }

    public Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default) =>
        Task.FromResult(new LoginResult(false, "FakeAuthService does not perform real login"));

    public Task<(RoomJoinedResult RoomJoined, ServerStatePayload ServerState)?> ReadInitialServerStateAsync(CancellationToken ct = default) =>
        Task.FromResult<(RoomJoinedResult, ServerStatePayload)?>(null);

    public Task SendLeaveRoomAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RegisterUdpAsync(uint clientId, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Configures the fake as a logged-in user.
    /// </summary>
    public void SetLoggedInUser(Guid userId, string username, bool isAdmin = false)
    {
        IsLoggedIn = true;
        User = new User { Id = userId, Username = username, Role = isAdmin ? UserRole.Admin : UserRole.User };
        Token = "fake-token";
        IsAdmin = isAdmin;
    }
}
