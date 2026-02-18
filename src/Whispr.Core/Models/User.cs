namespace Whispr.Core.Models;

/// <summary>
/// User roles for ACL. Admin can manage users and server settings.
/// </summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}

/// <summary>
/// Represents a user in the system.
/// </summary>
public sealed class User
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }

    /// <summary>
    /// Password hash (stored server-side only). Not sent over the wire.
    /// </summary>
    public string? PasswordHash { get; init; }

    /// <summary>
    /// Role for ACL. Admin can manage users and server settings.
    /// </summary>
    public UserRole Role { get; init; } = UserRole.User;
}
