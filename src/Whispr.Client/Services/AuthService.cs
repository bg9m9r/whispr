using Whispr.Core.Crypto;
using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Handles authentication and room operations via the control channel.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly ConnectionService _connection;
    private string? _token;
    private User? _user;
    private bool _isAdmin;

    public AuthService(ConnectionService connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Whether the user is logged in.
    /// </summary>
    public bool IsLoggedIn => _token is not null;

    /// <summary>
    /// Current user (after login).
    /// </summary>
    public User? User => _user;

    /// <summary>
    /// Session token for authenticated requests.
    /// </summary>
    public string? Token => _token;

    /// <summary>
    /// Whether the current user has admin permission (from server).
    /// </summary>
    public bool IsAdmin => _isAdmin;

    /// <summary>
    /// Logs in with username and password.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        await _connection.SendAsync(MessageTypes.LoginRequest, new LoginRequestPayload { Username = username, Password = password }, ct);
        var response = await ReadNextAsync(ct);
        if (response is null)
            return new LoginResult(false, "Connection closed");

        if (response.Type != MessageTypes.LoginResponse)
            return new LoginResult(false, "Unexpected response");

        var payload = ControlProtocol.DeserializePayload<LoginResponsePayload>(response);
        if (payload is null || !payload.Success)
            return new LoginResult(false, payload?.Error ?? "Invalid response");

        _token = payload.Token;
        var role = UserRole.User;
        if (string.Equals(payload.Role, "admin", StringComparison.OrdinalIgnoreCase))
            role = UserRole.Admin;
        _user = new User { Id = payload.UserId!.Value, Username = payload.Username ?? username, Role = role };
        _isAdmin = payload.IsAdmin;
        return new LoginResult(true);
    }

    /// <summary>
    /// After login, reads RoomJoined and ServerState (server auto-joins to default channel).
    /// </summary>
    public async Task<(ChannelJoinedResult ChannelJoined, ServerStatePayload ServerState)?> ReadInitialServerStateAsync(CancellationToken ct = default)
    {
        var roomJoined = await ReadNextAsync(ct);
        if (roomJoined?.Type != MessageTypes.RoomJoined)
            return null;

        var joinedPayload = ControlProtocol.DeserializePayload<RoomJoinedPayload>(roomJoined);
        if (joinedPayload is null)
            return null;

        var serverState = await ReadNextAsync(ct);
        if (serverState?.Type != MessageTypes.ServerState)
            return null;

        var statePayload = ControlProtocol.DeserializePayload<ServerStatePayload>(serverState);
        if (statePayload is null)
            return null;

        var key = joinedPayload.KeyMaterial is { Length: > 0 }
            ? KeyDerivation.DeriveAudioKey(joinedPayload.KeyMaterial)
            : null;
        var type = string.Equals(joinedPayload.Type, "text", StringComparison.OrdinalIgnoreCase) ? "text" : "voice";
        var members = joinedPayload.Members ?? joinedPayload.MemberIds.Select(id => new MemberInfo { UserId = id, Username = id.ToString(), ClientId = 0 }).ToList();
        var roomResult = new ChannelJoinedResult(joinedPayload.RoomId, joinedPayload.RoomName, type, joinedPayload.MemberIds, members, key);
        return (roomResult, statePayload);
    }

    /// <summary>
    /// Creates a room and joins it.
    /// </summary>
    public async Task<ChannelJoinedResult?> CreateRoomAsync(string name, CancellationToken ct = default)
    {
        RequireAuth();
        await _connection.SendAsync(MessageTypes.CreateRoom, new CreateRoomPayload { Name = name }, ct);
        return await WaitForRoomJoinedAsync(ct);
    }

    /// <summary>
    /// Joins an existing room.
    /// </summary>
    public async Task<ChannelJoinedResult?> JoinRoomAsync(Guid channelId, CancellationToken ct = default)
    {
        RequireAuth();
        await _connection.SendAsync(MessageTypes.JoinRoom, new JoinRoomPayload { RoomId = channelId }, ct);
        return await WaitForRoomJoinedAsync(ct);
    }

    /// <summary>
    /// Leaves the current room (waits for RoomLeft response).
    /// </summary>
    public async Task<bool> LeaveRoomAsync(CancellationToken ct = default)
    {
        RequireAuth();
        await _connection.SendAsync(MessageTypes.LeaveRoom, new { }, ct);
        var response = await ReadNextAsync(ct);
        if (response?.Type == MessageTypes.RoomLeft)
            return true;
        if (response?.Type == MessageTypes.Error)
            return false;
        return false;
    }

    /// <summary>
    /// Sends LeaveRoom without waiting for response. Use when a background reader will handle RoomLeft.
    /// </summary>
    public async Task SendLeaveRoomAsync(CancellationToken ct = default)
    {
        RequireAuth();
        await _connection.SendAsync(MessageTypes.LeaveRoom, new { }, ct);
    }

    /// <summary>
    /// Gets the list of available rooms from the server.
    /// </summary>
    public async Task<IReadOnlyList<RoomInfo>> GetRoomListAsync(CancellationToken ct = default)
    {
        RequireAuth();
        await _connection.SendAsync(MessageTypes.RequestRoomList, new { }, ct);
        var response = await ReadNextAsync(ct);
        if (response?.Type != MessageTypes.RoomList)
            return [];

        var payload = ControlProtocol.DeserializePayload<RoomListPayload>(response);
        return payload?.Rooms ?? [];
    }

    private void RequireAuth()
    {
        if (_token is null)
            throw new InvalidOperationException("Not logged in.");
    }

    private async Task<ControlMessage?> ReadNextAsync(CancellationToken ct)
    {
        while (true)
        {
            var msg = await _connection.ReadAsync(ct);
            if (msg is null) return null;
            if (msg.Type != MessageTypes.Pong) return msg;
        }
    }

    private async Task<ChannelJoinedResult?> WaitForRoomJoinedAsync(CancellationToken ct)
    {
        var response = await ReadNextAsync(ct);
        if (response?.Type == MessageTypes.Error)
        {
            var err = ControlProtocol.DeserializePayload<ErrorPayload>(response);
            throw new InvalidOperationException(err?.Message ?? "Room operation failed");
        }
        if (response?.Type != MessageTypes.RoomJoined)
            return null;

        var payload = ControlProtocol.DeserializePayload<RoomJoinedPayload>(response);
        if (payload is null)
            return null;

        var key = payload.KeyMaterial is { Length: > 0 }
            ? KeyDerivation.DeriveAudioKey(payload.KeyMaterial)
            : null;
        var type = string.Equals(payload.Type, "text", StringComparison.OrdinalIgnoreCase) ? "text" : "voice";
        var members = payload.Members ?? payload.MemberIds.Select(id => new MemberInfo { UserId = id, Username = id.ToString(), ClientId = 0 }).ToList();
        return new ChannelJoinedResult(payload.RoomId, payload.RoomName, type, payload.MemberIds, members, key);
    }
}

/// <summary>
/// Result of a login attempt.
/// </summary>
public sealed record LoginResult(bool Success, string? Error = null);

/// <summary>
/// Result of joining or creating a channel (wire: room_joined).
/// </summary>
/// <param name="ChannelType">"voice" or "text".</param>
/// <param name="AudioKey">Null for text channels (no audio).</param>
public sealed record ChannelJoinedResult(Guid ChannelId, string ChannelName, string ChannelType, IReadOnlyList<Guid> MemberIds, IReadOnlyList<MemberInfo> Members, byte[]? AudioKey);
