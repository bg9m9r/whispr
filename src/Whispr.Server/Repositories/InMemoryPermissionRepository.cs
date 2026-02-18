namespace Whispr.Server.Repositories;

/// <summary>
/// In-memory permission repository for development/testing when no database is configured.
/// Returns empty for all queries; channel access is always allowed (public).
/// </summary>
public sealed class InMemoryPermissionRepository : IPermissionRepository
{
    public IReadOnlyList<(string Id, string Name, string? Description)> ListPermissions() => [];

    public IReadOnlyList<(string Id, string Name, IReadOnlyList<(string PermissionId, int State)> Permissions)> ListRoles() => [];

    public (IReadOnlyList<(string PermissionId, int State)> Permissions, IReadOnlyList<string> RoleIds) GetUserPermissions(Guid userId) => ([], []);

    public bool SetUserPermission(Guid userId, string permissionId, int? state) => false;

    public bool SetUserRole(Guid userId, string roleId, bool assign) => false;

    public bool CanAccessChannel(Guid userId, Guid channelId, bool isAdmin) => true;

    public (IReadOnlyList<(string RoleId, int State)> RoleStates, IReadOnlyList<(Guid UserId, int State)> UserStates) GetChannelPermissions(Guid channelId) => ([], []);

    public bool SetChannelRolePermission(Guid channelId, string roleId, int? state) => false;

    public bool SetChannelUserPermission(Guid channelId, Guid userId, int? state) => false;
}
