namespace Whispr.Server.Repositories;

/// <summary>
/// Repository for permission and role data access.
/// Handles Permissions, Roles, UserPermissions, UserRoles, ChannelRolePermissions, ChannelUserPermissions.
/// </summary>
public interface IPermissionRepository
{
    /// <summary>
    /// Lists all permissions.
    /// </summary>
    IReadOnlyList<(string Id, string Name, string? Description)> ListPermissions();

    /// <summary>
    /// Lists all roles with their permission assignments.
    /// </summary>
    IReadOnlyList<(string Id, string Name, IReadOnlyList<(string PermissionId, int State)> Permissions)> ListRoles();

    /// <summary>
    /// Gets a user's direct permission assignments and role assignments.
    /// </summary>
    (IReadOnlyList<(string PermissionId, int State)> Permissions, IReadOnlyList<string> RoleIds) GetUserPermissions(Guid userId);

    /// <summary>
    /// Sets a direct permission for a user. State: 0=Allow, 1=Deny, 2=Neutral. Null removes.
    /// </summary>
    bool SetUserPermission(Guid userId, string permissionId, int? state);

    /// <summary>
    /// Assigns or removes a role for a user.
    /// </summary>
    bool SetUserRole(Guid userId, string roleId, bool assign);

    /// <summary>
    /// Returns true if the user can access the channel. Channels with no permissions are public.
    /// Deny overrides Allow. Admins bypass channel restrictions (caller passes isAdmin).
    /// </summary>
    bool CanAccessChannel(Guid userId, Guid channelId, bool isAdmin);

    /// <summary>
    /// Gets channel permissions: role assignments and user overrides for channel_access.
    /// </summary>
    (IReadOnlyList<(string RoleId, int State)> RoleStates, IReadOnlyList<(Guid UserId, int State)> UserStates) GetChannelPermissions(Guid channelId);

    /// <summary>
    /// Sets a role's channel_access permission for a channel. State: 0=Allow, 1=Deny, 2=Neutral. Null removes.
    /// </summary>
    bool SetChannelRolePermission(Guid channelId, string roleId, int? state);

    /// <summary>
    /// Sets a user's channel_access permission for a channel. State: 0=Allow, 1=Deny, 2=Neutral. Null removes.
    /// </summary>
    bool SetChannelUserPermission(Guid channelId, Guid userId, int? state);
}
