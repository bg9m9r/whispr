using System.Collections.Concurrent;
using Whispr.Core.Crypto;
using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Manages the control channel reader, server state, room operations, and permissions protocol.
/// Exposes events for UI updates; encapsulates TaskCompletionSource handling for request/response.
/// </summary>
public sealed class ChannelService : IChannelService
{
    private readonly ConnectionService _connection;
    private readonly IAuthService _auth;
    private readonly Guid _myUserId;
    private readonly Action<Action> _postToUi;
    private readonly ConcurrentDictionary<Guid, string> _members = new();
    private readonly ConcurrentDictionary<Guid, uint> _userIdToClientId = new();
    private CancellationTokenSource? _readerCts;
    private CancellationTokenSource? _pingCts;
    private TaskCompletionSource<bool>? _pendingRoomLeftTcs;
    private TaskCompletionSource<ChannelJoinedResult?>? _pendingRoomJoinedTcs;
    private TaskCompletionSource<object?>? _pendingPermissionResponseTcs;
    private TaskCompletionSource<uint>? _pendingRegisterUdpTcs;
    private bool _disposed;
    private int? _pingLatencyMs;
    private DateTime _pendingPingSentAt;
    private readonly object _pingLock = new();

    private const int PingIntervalMs = 5000;
    private const int PingTimeoutMs = 6000;
    private const int MaxValidRttMs = 10000;

    /// <param name="postToUi">Invokes the action on the UI thread (e.g. Dispatcher.UIThread.Post).</param>
    public ChannelService(ConnectionService connection, IAuthService auth, Guid myUserId, Action<Action> postToUi)
    {
        _connection = connection;
        _auth = auth;
        _myUserId = myUserId;
        _postToUi = postToUi;
    }

    /// <summary>
    /// Current server state (channels, members). Updated when ServerState message received.
    /// </summary>
    public ServerStatePayload ServerState { get; private set; } = new() { Channels = [] };

    /// <summary>
    /// Current room (id, name, key, members). Updated when RoomJoined received.
    /// </summary>
    public ChannelJoinedResult ChannelResult { get; private set; } = null!;

    /// <summary>
    /// UserId -> ClientId for UDP. Updated from MemberJoined, MemberUdpRegistered, MemberLeft.
    /// </summary>
    public IReadOnlyDictionary<Guid, uint> UserIdToClientId => _userIdToClientId;

    /// <summary>
    /// UserId -> Username lookup. Updated from room result and member messages.
    /// </summary>
    public IReadOnlyDictionary<Guid, string> Members => _members;

    /// <summary>
    /// Raised when ServerState is received. Subscribe on UI thread if updating UI.
    /// </summary>
    public event Action<ServerStatePayload>? ServerStateReceived;

    /// <summary>
    /// Raised when RoomJoined is received (join or switch). Subscribe on UI thread.
    /// </summary>
    public event Action<ChannelJoinedResult>? RoomJoinedReceived;

    /// <summary>
    /// Raised when RoomLeft is received and we're not switching. Subscribe on UI thread.
    /// </summary>
    public event Action? RoomLeftReceived;

    /// <summary>
    /// Last measured round-trip latency in ms. Null = unknown, -1 = timeout (no response).
    /// </summary>
    public int? PingLatencyMs => _pingLatencyMs;

    /// <summary>
    /// Raised when ping latency is updated. Value: ms, or -1 for timeout, or null when reset.
    /// </summary>
    public event Action<int?>? PingLatencyUpdated;

    /// <summary>
    /// Initializes with initial room and state, then starts the control reader.
    /// </summary>
    public void Start(ChannelJoinedResult roomResult, ServerStatePayload serverState)
    {
        ChannelResult = roomResult;
        ServerState = serverState;
        _members.Clear();
        _userIdToClientId.Clear();
        foreach (var m in roomResult.Members)
        {
            _members[m.UserId] = m.Username;
            if (m.ClientId != 0)
                _userIdToClientId[m.UserId] = m.ClientId;
        }
        _readerCts = new CancellationTokenSource();
        _pingCts = new CancellationTokenSource();
        _ = RunControlReaderAsync(_readerCts.Token);
        _ = RunPingLoopAsync(_pingCts.Token);
    }

    /// <summary>
    /// Stops the control reader.
    /// </summary>
    public void Stop()
    {
        _readerCts?.Cancel();
        _pingCts?.Cancel();
    }

    private async Task RunPingLoopAsync(CancellationToken ct)
    {
        try
        {
            var first = true;
            while (!ct.IsCancellationRequested && !_disposed)
            {
                if (!first)
                {
                    await Task.Delay(PingIntervalMs, ct);
                    if (ct.IsCancellationRequested || _disposed) break;

                    lock (_pingLock)
                    {
                        if (_pendingPingSentAt != default && (DateTime.UtcNow - _pendingPingSentAt).TotalMilliseconds > PingTimeoutMs)
                        {
                            _pendingPingSentAt = default;
                            _pingLatencyMs = -1;
                            _postToUi(() => PingLatencyUpdated?.Invoke(-1));
                        }
                    }
                }
                first = false;

                try
                {
                    await _connection.SendAsync(MessageTypes.Ping, new { }, ct);
                    lock (_pingLock)
                    {
                        _pendingPingSentAt = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    ClientLog.Info($"Ping send failed: {ex.Message}");
                    lock (_pingLock)
                    {
                        _pendingPingSentAt = default;
                        _pingLatencyMs = -1;
                        _postToUi(() => PingLatencyUpdated?.Invoke(-1));
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Registers UDP with server; server assigns and returns client ID.
    /// </summary>
    public async Task<uint> RegisterUdpAsync(CancellationToken ct = default)
    {
        _pendingRegisterUdpTcs = new TaskCompletionSource<uint>();
        await _connection.SendAsync(MessageTypes.RegisterUdp, new object(), ct);
        return await _pendingRegisterUdpTcs.Task.WaitAsync(ct);
    }

    /// <summary>
    /// Requests server state. ServerStateReceived will be raised when response arrives.
    /// </summary>
    public async Task RequestServerStateAsync()
    {
        try
        {
            await _connection.SendAsync(MessageTypes.RequestServerState, new { });
        }
        catch (Exception ex)
        {
            ClientLog.Info($"RequestServerState failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Leaves current room and joins the specified channel.
    /// </summary>
    public async Task<ChannelJoinedResult?> SwitchToChannelAsync(Guid channelId)
    {
        _pendingRoomLeftTcs = new TaskCompletionSource<bool>();
        await _auth.SendLeaveRoomAsync();
        await _pendingRoomLeftTcs.Task;
        _pendingRoomLeftTcs = null;

        _pendingRoomJoinedTcs = new TaskCompletionSource<ChannelJoinedResult?>();
        await _connection.SendAsync(MessageTypes.JoinRoom, new JoinRoomPayload { RoomId = channelId });
        var result = await _pendingRoomJoinedTcs.Task;
        _pendingRoomJoinedTcs = null;
        return result;
    }

    /// <summary>
    /// Creates a new channel and joins it.
    /// </summary>
    public async Task<ChannelJoinedResult?> CreateChannelAsync(string name)
    {
        _pendingRoomJoinedTcs = new TaskCompletionSource<ChannelJoinedResult?>();
        await _connection.SendAsync(MessageTypes.CreateRoom, new CreateRoomPayload { Name = name });
        var result = await _pendingRoomJoinedTcs.Task;
        _pendingRoomJoinedTcs = null;
        return result;
    }

    /// <summary>
    /// Leaves the current room.
    /// </summary>
    public async Task LeaveRoomAsync()
    {
        await _auth.SendLeaveRoomAsync();
    }

    // --- Permissions protocol ---

    public async Task<PermissionsListPayload?> RequestPermissionsListAsync()
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.ListPermissions, new { });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as PermissionsListPayload;
    }

    public async Task<RolesListPayload?> RequestRolesListAsync()
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.ListRoles, new { });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as RolesListPayload;
    }

    public async Task<UserPermissionsPayload?> RequestUserPermissionsAsync(Guid userId)
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.GetUserPermissions, new GetUserPermissionsPayload { UserId = userId });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as UserPermissionsPayload;
    }

    public async Task<UserPermissionsPayload?> SetUserPermissionAsync(Guid userId, string permissionId, string? state)
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.SetUserPermission, new SetUserPermissionPayload { UserId = userId, PermissionId = permissionId, State = state });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as UserPermissionsPayload;
    }

    public async Task<UserPermissionsPayload?> SetUserRoleAsync(Guid userId, string roleId, bool assign)
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.SetUserRole, new SetUserRolePayload { UserId = userId, RoleId = roleId, Assign = assign });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as UserPermissionsPayload;
    }

    public async Task<ChannelPermissionsPayload?> RequestChannelPermissionsAsync(Guid channelId)
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.GetChannelPermissions, new GetChannelPermissionsPayload { ChannelId = channelId });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as ChannelPermissionsPayload;
    }

    public async Task<ChannelPermissionsPayload?> SetChannelRolePermissionAsync(Guid channelId, string roleId, string? state)
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.SetChannelRolePermission, new SetChannelRolePermissionPayload { ChannelId = channelId, RoleId = roleId, State = state });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as ChannelPermissionsPayload;
    }

    public async Task<ChannelPermissionsPayload?> SetChannelUserPermissionAsync(Guid channelId, Guid userId, string? state)
    {
        _pendingPermissionResponseTcs = new TaskCompletionSource<object?>();
        await _connection.SendAsync(MessageTypes.SetChannelUserPermission, new SetChannelUserPermissionPayload { ChannelId = channelId, UserId = userId, State = state });
        var result = await _pendingPermissionResponseTcs.Task;
        _pendingPermissionResponseTcs = null;
        return result as ChannelPermissionsPayload;
    }

    /// <summary>
    /// Gets username for a user ID from members or server state.
    /// </summary>
    public string? GetUsernameForUserId(Guid userId)
    {
        if (_members.TryGetValue(userId, out var name)) return name;
        foreach (var ch in ServerState.Channels)
        {
            var m = ch.Members?.FirstOrDefault(x => x.UserId == userId);
            if (m is not null) return m.Username;
        }
        return null;
    }

    /// <summary>
    /// Gets members of a channel from server state.
    /// </summary>
    public IReadOnlyList<MemberInfo> GetChannelMembers(Guid channelId)
    {
        var ch = ServerState.Channels.FirstOrDefault(c => c.Id == channelId);
        return ch?.Members ?? [];
    }

    private async Task RunControlReaderAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await _connection.ReadAsync(ct);
                if (msg is null) break;

                switch (msg.Type)
                {
                    case MessageTypes.MemberJoined:
                        var joined = ControlProtocol.DeserializePayload<MemberPayload>(msg);
                        if (joined is not null)
                        {
                            _members[joined.UserId] = joined.Username;
                            if (joined.ClientId != 0)
                                _userIdToClientId[joined.UserId] = joined.ClientId;
                            _ = RequestServerStateAsync();
                        }
                        break;
                    case MessageTypes.RegisterUdpResponse:
                        var regResp = ControlProtocol.DeserializePayload<RegisterUdpResponsePayload>(msg);
                        if (regResp is not null && regResp.ClientId != 0)
                            _pendingRegisterUdpTcs?.TrySetResult(regResp.ClientId);
                        _pendingRegisterUdpTcs = null;
                        break;
                    case MessageTypes.MemberUdpRegistered:
                        var registered = ControlProtocol.DeserializePayload<MemberPayload>(msg);
                        if (registered is not null && registered.ClientId != 0)
                        {
                            _members[registered.UserId] = registered.Username;
                            _userIdToClientId[registered.UserId] = registered.ClientId;
                            _ = RequestServerStateAsync();
                        }
                        break;
                    case MessageTypes.MemberLeft:
                        var left = ControlProtocol.DeserializePayload<MemberPayload>(msg);
                        if (left is not null)
                        {
                            _members.TryRemove(left.UserId, out _);
                            _userIdToClientId.TryRemove(left.UserId, out _);
                            _ = RequestServerStateAsync();
                        }
                        break;
                    case MessageTypes.Pong:
                        lock (_pingLock)
                        {
                            if (_pendingPingSentAt != default)
                            {
                                var rttMs = (int)(DateTime.UtcNow - _pendingPingSentAt).TotalMilliseconds;
                                _pendingPingSentAt = default;
                                if (rttMs >= 0 && rttMs < MaxValidRttMs)
                                {
                                    _pingLatencyMs = rttMs;
                                    var v = rttMs;
                                    _postToUi(() => PingLatencyUpdated?.Invoke(v));
                                }
                            }
                        }
                        break;
                    case MessageTypes.RoomJoined:
                        var roomJoined = ControlProtocol.DeserializePayload<RoomJoinedPayload>(msg);
                        if (roomJoined is not null)
                        {
                            var key = KeyDerivation.DeriveAudioKey(roomJoined.KeyMaterial);
                            var members = roomJoined.Members ?? roomJoined.MemberIds.Select(id => new MemberInfo { UserId = id, Username = id.ToString(), ClientId = 0 }).ToList();
                            var result = new ChannelJoinedResult(roomJoined.RoomId, roomJoined.RoomName, roomJoined.MemberIds, members, key);

                            _pendingRoomJoinedTcs?.TrySetResult(result);
                            ChannelResult = result;
                            _members.Clear();
                            _userIdToClientId.Clear();
                            foreach (var m in result.Members)
                            {
                                _members[m.UserId] = m.Username;
                                if (m.ClientId != 0)
                                    _userIdToClientId[m.UserId] = m.ClientId;
                            }
                            var r = result;
                            _postToUi(() => RoomJoinedReceived?.Invoke(r));
                        }
                        break;
                    case MessageTypes.ServerState:
                        var state = ControlProtocol.DeserializePayload<ServerStatePayload>(msg);
                        if (state is not null)
                        {
                            ServerState = state;
                            var s = state;
                            _postToUi(() => ServerStateReceived?.Invoke(s));
                        }
                        break;
                    case MessageTypes.RoomLeft:
                        var wasSwitching = _pendingRoomLeftTcs is not null;
                        _pendingRoomLeftTcs?.TrySetResult(true);
                        if (!wasSwitching)
                        {
                            _postToUi(() => RoomLeftReceived?.Invoke());
                            return;
                        }
                        break;
                    case MessageTypes.Error:
                        var err = ControlProtocol.DeserializePayload<ErrorPayload>(msg);
                        _pendingRoomJoinedTcs?.TrySetResult(null);
                        _pendingRoomLeftTcs?.TrySetResult(false);
                        _pendingPermissionResponseTcs?.TrySetResult(null);
                        _pendingRegisterUdpTcs?.TrySetException(new InvalidOperationException(err?.Message ?? "Server error"));
                        ClientLog.Info($"Server error: {err?.Message}");
                        break;
                    case MessageTypes.PermissionsList:
                        var permsList = ControlProtocol.DeserializePayload<PermissionsListPayload>(msg);
                        _pendingPermissionResponseTcs?.TrySetResult(permsList);
                        _pendingPermissionResponseTcs = null;
                        break;
                    case MessageTypes.RolesList:
                        var rolesList = ControlProtocol.DeserializePayload<RolesListPayload>(msg);
                        _pendingPermissionResponseTcs?.TrySetResult(rolesList);
                        _pendingPermissionResponseTcs = null;
                        break;
                    case MessageTypes.UserPermissions:
                        var userPerms = ControlProtocol.DeserializePayload<UserPermissionsPayload>(msg);
                        _pendingPermissionResponseTcs?.TrySetResult(userPerms);
                        _pendingPermissionResponseTcs = null;
                        break;
                    case MessageTypes.ChannelPermissions:
                        var channelPerms = ControlProtocol.DeserializePayload<ChannelPermissionsPayload>(msg);
                        _pendingPermissionResponseTcs?.TrySetResult(channelPerms);
                        _pendingPermissionResponseTcs = null;
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ClientLog.Info($"Control reader: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _readerCts?.Cancel();
        _pingCts?.Cancel();
        _disposed = true;
    }
}
