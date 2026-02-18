using Whispr.Client.Services;
using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// Configurable fake IChannelService for unit testing.
/// Returns preset data for permission and channel operations.
/// </summary>
public sealed class FakeChannelService : IChannelService
{
    private ServerStatePayload _serverState = new() { Channels = [] };
    private RoomJoinedResult? _roomResult;

    public ServerStatePayload ServerState
    {
        get => _serverState;
        set => _serverState = value;
    }

    public RoomJoinedResult RoomResult =>
        _roomResult ?? throw new InvalidOperationException("RoomResult not set. Call SetRoomResult first.");

    public IReadOnlyDictionary<Guid, uint> UserIdToClientId { get; } = new Dictionary<Guid, uint>();
    public IReadOnlyDictionary<Guid, string> Members { get; } = new Dictionary<Guid, string>();

    public event Action<ServerStatePayload>? ServerStateReceived;
    public event Action<RoomJoinedResult>? RoomJoinedReceived;
    public event Action? RoomLeftReceived;
    public event Action<int?>? PingLatencyUpdated;

    public int? PingLatencyMs => null;

    private PermissionsListPayload? _permissionsList;
    private RolesListPayload? _rolesList;
    private readonly Dictionary<Guid, UserPermissionsPayload> _userPermissions = new();
    private readonly Dictionary<Guid, ChannelPermissionsPayload> _channelPermissions = new();

    public void SetRoomResult(RoomJoinedResult result) => _roomResult = result;

    public void SetPermissions(PermissionsListPayload payload) => _permissionsList = payload;
    public void SetRoles(RolesListPayload payload) => _rolesList = payload;
    public void SetUserPermissions(Guid userId, UserPermissionsPayload payload) => _userPermissions[userId] = payload;
    public void SetChannelPermissions(Guid channelId, ChannelPermissionsPayload payload) => _channelPermissions[channelId] = payload;

    public void Start(RoomJoinedResult roomResult, ServerStatePayload serverState)
    {
        _roomResult = roomResult;
        _serverState = serverState;
    }

    public void Stop() { }

    public Task RequestServerStateAsync() => Task.CompletedTask;

    public Task<RoomJoinedResult?> SwitchToChannelAsync(Guid channelId) =>
        Task.FromResult<RoomJoinedResult?>(_roomResult);

    public Task<RoomJoinedResult?> CreateChannelAsync(string name) =>
        Task.FromResult<RoomJoinedResult?>(_roomResult);

    public Task LeaveRoomAsync() => Task.CompletedTask;

    public Task<PermissionsListPayload?> RequestPermissionsListAsync() =>
        Task.FromResult(_permissionsList);

    public Task<RolesListPayload?> RequestRolesListAsync() =>
        Task.FromResult(_rolesList);

    public Task<UserPermissionsPayload?> RequestUserPermissionsAsync(Guid userId) =>
        Task.FromResult(_userPermissions.TryGetValue(userId, out var p) ? p : null);

    public Task<UserPermissionsPayload?> SetUserPermissionAsync(Guid userId, string permissionId, string? state)
    {
        if (!_userPermissions.TryGetValue(userId, out var p))
            p = new UserPermissionsPayload { UserId = userId, Permissions = [], RoleIds = [] };
        var perms = p.Permissions.Where(x => x.PermissionId != permissionId).ToList();
        if (state is not null)
            perms.Add(new UserPermissionAssignment { PermissionId = permissionId, State = state });
        _userPermissions[userId] = new UserPermissionsPayload { UserId = userId, Permissions = perms, RoleIds = p.RoleIds };
        return Task.FromResult<UserPermissionsPayload?>(_userPermissions[userId]);
    }

    public Task<UserPermissionsPayload?> SetUserRoleAsync(Guid userId, string roleId, bool assign)
    {
        if (!_userPermissions.TryGetValue(userId, out var p))
            p = new UserPermissionsPayload { UserId = userId, Permissions = [], RoleIds = [] };
        var roles = p.RoleIds.Where(r => r != roleId).ToList();
        if (assign)
            roles.Add(roleId);
        _userPermissions[userId] = new UserPermissionsPayload { UserId = userId, Permissions = p.Permissions, RoleIds = roles };
        return Task.FromResult<UserPermissionsPayload?>(_userPermissions[userId]);
    }

    public Task<ChannelPermissionsPayload?> RequestChannelPermissionsAsync(Guid channelId) =>
        Task.FromResult(_channelPermissions.TryGetValue(channelId, out var p) ? p : null);

    public Task<ChannelPermissionsPayload?> SetChannelRolePermissionAsync(Guid channelId, string roleId, string? state)
    {
        if (!_channelPermissions.TryGetValue(channelId, out var p))
            p = new ChannelPermissionsPayload { ChannelId = channelId, RoleStates = [], UserStates = [] };
        var roles = p.RoleStates.Where(r => r.RoleId != roleId).ToList();
        if (state is not null)
            roles.Add(new ChannelRoleState { RoleId = roleId, State = state });
        _channelPermissions[channelId] = new ChannelPermissionsPayload { ChannelId = channelId, RoleStates = roles, UserStates = p.UserStates };
        return Task.FromResult<ChannelPermissionsPayload?>(_channelPermissions[channelId]);
    }

    public Task<ChannelPermissionsPayload?> SetChannelUserPermissionAsync(Guid channelId, Guid userId, string? state)
    {
        if (!_channelPermissions.TryGetValue(channelId, out var p))
            p = new ChannelPermissionsPayload { ChannelId = channelId, RoleStates = [], UserStates = [] };
        var users = p.UserStates.Where(u => u.UserId != userId).ToList();
        if (state is not null)
            users.Add(new ChannelUserState { UserId = userId, State = state });
        _channelPermissions[channelId] = new ChannelPermissionsPayload { ChannelId = channelId, RoleStates = p.RoleStates, UserStates = users };
        return Task.FromResult<ChannelPermissionsPayload?>(_channelPermissions[channelId]);
    }

    private readonly Dictionary<Guid, string> _usernameCache = new();

    public string? GetUsernameForUserId(Guid userId)
    {
        if (_usernameCache.TryGetValue(userId, out var name)) return name;
        foreach (var ch in _serverState.Channels)
        {
            var m = ch.Members?.FirstOrDefault(x => x.UserId == userId);
            if (m is not null) return m.Username;
        }
        return null;
    }

    public void SetUsernameForUserId(Guid userId, string username) => _usernameCache[userId] = username;

    public IReadOnlyList<MemberInfo> GetChannelMembers(Guid channelId)
    {
        var ch = _serverState.Channels.FirstOrDefault(c => c.Id == channelId);
        return ch?.Members ?? [];
    }

    public void Dispose() { }
}
