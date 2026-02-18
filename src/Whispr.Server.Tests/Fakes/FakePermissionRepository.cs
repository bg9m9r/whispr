using System.Collections.Concurrent;

namespace Whispr.Server.Tests.Fakes;

/// <summary>
/// Configurable in-memory permission repository for unit testing AuthService permission logic.
/// Supports permissions, roles, user permissions, user roles, and channel access.
/// </summary>
public sealed class FakePermissionRepository : Repositories.IPermissionRepository
{
    private const string ChannelAccessPermissionId = "channel_access";

    private readonly ConcurrentDictionary<string, (string Name, string? Description)> _permissions = new();
    private readonly ConcurrentDictionary<string, string> _roles = new();
    private readonly ConcurrentDictionary<(string RoleId, string PermissionId), int> _rolePermissions = new();
    private readonly ConcurrentDictionary<(string UserId, string PermissionId), int> _userPermissions = new();
    private readonly ConcurrentDictionary<(string UserId, string RoleId), byte> _userRoles = new();
    private readonly ConcurrentDictionary<(string ChannelId, string RoleId), int> _channelRolePermissions = new();
    private readonly ConcurrentDictionary<(string ChannelId, string UserId), int> _channelUserPermissions = new();

    public void AddPermission(string id, string name, string? description = null) =>
        _permissions[id] = (name, description);

    public void AddRole(string id, string name) =>
        _roles[id] = name;

    public void SetRolePermission(string roleId, string permissionId, int state) =>
        _rolePermissions[(roleId, permissionId)] = state;

    public IReadOnlyList<(string Id, string Name, string? Description)> ListPermissions() =>
        _permissions.Select(p => (p.Key, p.Value.Name, p.Value.Description)).ToList();

    public IReadOnlyList<(string Id, string Name, IReadOnlyList<(string PermissionId, int State)> Permissions)> ListRoles()
    {
        return _roles.Keys.Select(roleId =>
        {
            var perms = _rolePermissions
                .Where(kv => kv.Key.RoleId == roleId)
                .Select(kv => (kv.Key.PermissionId, kv.Value))
                .ToList();
            return (roleId, _roles[roleId], (IReadOnlyList<(string, int)>)perms);
        }).ToList();
    }

    public (IReadOnlyList<(string PermissionId, int State)> Permissions, IReadOnlyList<string> RoleIds) GetUserPermissions(Guid userId)
    {
        var uid = userId.ToString();
        var perms = _userPermissions
            .Where(kv => kv.Key.UserId == uid)
            .Select(kv => (kv.Key.PermissionId, kv.Value))
            .ToList();
        var roleIds = _userRoles
            .Where(kv => kv.Key.UserId == uid)
            .Select(kv => kv.Key.RoleId)
            .ToList();
        return (perms, roleIds);
    }

    public bool SetUserPermission(Guid userId, string permissionId, int? state)
    {
        var key = (userId.ToString(), permissionId);
        if (state is null)
            return _userPermissions.TryRemove(key, out _);
        _userPermissions[key] = state.Value;
        return true;
    }

    public bool SetUserRole(Guid userId, string roleId, bool assign)
    {
        var key = (userId.ToString(), roleId);
        if (assign)
        {
            _userRoles[key] = 0;
            return true;
        }
        return _userRoles.TryRemove(key, out _);
    }

    public bool CanAccessChannel(Guid userId, Guid channelId, bool isAdmin)
    {
        if (isAdmin) return true;
        var cid = channelId.ToString();
        var uid = userId.ToString();

        var hasAnyChannelPerms = _channelRolePermissions.Keys.Any(k => k.ChannelId == cid) ||
                                 _channelUserPermissions.Keys.Any(k => k.ChannelId == cid);
        if (!hasAnyChannelPerms) return true;

        var states = new List<int>();
        if (_channelUserPermissions.TryGetValue((cid, uid), out var userState))
            states.Add(userState);
        foreach (var roleId in _userRoles.Where(kv => kv.Key.UserId == uid).Select(kv => kv.Key.RoleId))
        {
            if (_channelRolePermissions.TryGetValue((cid, roleId), out var roleState))
                states.Add(roleState);
        }
        if (states.Count == 0) return true;
        if (states.Contains(1)) return false;
        return states.Contains(0);
    }

    public (IReadOnlyList<(string RoleId, int State)> RoleStates, IReadOnlyList<(Guid UserId, int State)> UserStates) GetChannelPermissions(Guid channelId)
    {
        var cid = channelId.ToString();
        var roleStates = _channelRolePermissions
            .Where(kv => kv.Key.ChannelId == cid)
            .Select(kv => (kv.Key.RoleId, kv.Value))
            .ToList();
        var userStates = _channelUserPermissions
            .Where(kv => kv.Key.ChannelId == cid)
            .Select(kv => (Guid.Parse(kv.Key.UserId), kv.Value))
            .ToList();
        return (roleStates, userStates);
    }

    public bool SetChannelRolePermission(Guid channelId, string roleId, int? state)
    {
        var key = (channelId.ToString(), roleId);
        if (state is null)
            return _channelRolePermissions.TryRemove(key, out _);
        _channelRolePermissions[key] = state.Value;
        return true;
    }

    public bool SetChannelUserPermission(Guid channelId, Guid userId, int? state)
    {
        var key = (channelId.ToString(), userId.ToString());
        if (state is null)
            return _channelUserPermissions.TryRemove(key, out _);
        _channelUserPermissions[key] = state.Value;
        return true;
    }
}
