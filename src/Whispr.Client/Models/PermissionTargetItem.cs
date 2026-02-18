namespace Whispr.Client.Models;

/// <summary>
/// Represents a user in the Permissions context menu submenu (for channel members).
/// </summary>
public sealed record PermissionTargetItem(Guid UserId, string Username);
