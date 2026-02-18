using Whispr.Core.Models;

namespace Whispr.Server.Services;

/// <summary>
/// Authentication and authorization service.
/// Handles credentials, sessions, permissions, and channel access.
/// </summary>
public interface IAuthService
{
    User? ValidateCredentials(string username, string password);
    User? ValidateOrRegister(string username, string password);
    string IssueSessionToken(User user);
    User? ValidateToken(string? token);
    void RevokeToken(string token);
    bool IsAdmin(Guid userId);
    bool HasPermission(Guid userId, string permissionId);
    bool CanAccessChannel(Guid userId, Guid channelId);
    string? GetUsername(Guid userId);
    bool AddUser(string username, string password, UserRole role);

    IReadOnlyList<(string Id, string Name, string? Description)> ListPermissions();
    IReadOnlyList<(string Id, string Name, IReadOnlyList<(string PermissionId, int State)> Permissions)> ListRoles();
    (IReadOnlyList<(string PermissionId, int State)> Permissions, IReadOnlyList<string> RoleIds) GetUserPermissions(Guid userId);
    bool SetUserPermission(Guid userId, string permissionId, int? state);
    bool SetUserRole(Guid userId, string roleId, bool assign);
    (IReadOnlyList<(string RoleId, int State)> RoleStates, IReadOnlyList<(Guid UserId, int State)> UserStates) GetChannelPermissions(Guid channelId);
    bool SetChannelRolePermission(Guid channelId, string roleId, int? state);
    bool SetChannelUserPermission(Guid channelId, Guid userId, int? state);
}
