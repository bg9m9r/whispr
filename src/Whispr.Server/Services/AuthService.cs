using System.Collections.Concurrent;
using System.Security.Cryptography;
using Whispr.Core.Models;
using Whispr.Server.Repositories;

namespace Whispr.Server.Services;

/// <summary>
/// Authentication and authorization service.
/// Uses repositories for data access; keeps sessions in memory.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IPermissionRepository _permissionRepo;
    private readonly ConcurrentDictionary<string, (User User, DateTime IssuedAt)> _sessions = new();

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    /// <summary>
    /// Token lifetime. Tokens expire after this duration.
    /// </summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public AuthService(IUserRepository userRepo, IPermissionRepository permissionRepo)
    {
        _userRepo = userRepo;
        _permissionRepo = permissionRepo;
        SeedTestUsersIfEmpty();
    }

    private void SeedTestUsersIfEmpty()
    {
        if (_userRepo.LoadAll().Count > 0)
            return;
        AddUserInternal("admin", "admin", UserRole.Admin);
        AddUserInternal("bob", "bob", UserRole.User);
    }

    private void AddUserInternal(string username, string password, UserRole role)
    {
        var (hash, _) = HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = Convert.ToBase64String(hash),
            Role = role
        };
        _userRepo.Insert(user);
    }

    public User? ValidateCredentials(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = _userRepo.GetByUsername(username);
        if (user?.PasswordHash is null)
            return null;

        return VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    public User? ValidateOrRegister(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var existing = _userRepo.GetByUsername(username);
        if (existing is not null && existing.PasswordHash is not null)
            return VerifyPassword(password, existing.PasswordHash) ? existing : null;

        var added = AddUser(username, password, UserRole.User);
        return added ? _userRepo.GetByUsername(username) : null;
    }

    public string IssueSessionToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = (user, DateTime.UtcNow);
        return token;
    }

    public User? ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        if (!_sessions.TryGetValue(token, out var session))
            return null;
        if (DateTime.UtcNow - session.IssuedAt > TokenLifetime)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }
        return session.User;
    }

    public void RevokeToken(string token) => _sessions.TryRemove(token, out _);

    public bool IsAdmin(Guid userId)
    {
        if (HasPermissionFromStore(userId, "admin"))
            return true;
        var user = _userRepo.GetById(userId);
        return user?.Role == UserRole.Admin;
    }

    public bool HasPermission(Guid userId, string permissionId)
    {
        if (HasPermissionFromStore(userId, permissionId))
            return true;
        switch (permissionId)
        {
            case "admin":
            {
                var user = _userRepo.GetById(userId);
                return user?.Role == UserRole.Admin;
            }
            default:
                return false;
        }
    }

    private bool HasPermissionFromStore(Guid userId, string permissionId)
    {
        var (directPerms, roleIds) = _permissionRepo.GetUserPermissions(userId);
        var states = new List<int>();
        foreach (var (pid, state) in directPerms)
            if (pid == permissionId) states.Add(state);
        foreach (var roleId in roleIds)
        {
            var roles = _permissionRepo.ListRoles();
            var role = roles.FirstOrDefault(r => r.Id == roleId);
            var perms = role.Permissions ?? [];
            foreach (var (pid, state) in perms)
                if (pid == permissionId) states.Add(state);
        }
        if (states.Contains(1)) return false;
        return states.Contains(0);
    }

    public bool CanAccessChannel(Guid userId, Guid channelId) =>
        _permissionRepo.CanAccessChannel(userId, channelId, IsAdmin(userId));

    public string? GetUsername(Guid userId) => _userRepo.GetById(userId)?.Username;

    public bool AddUser(string username, string password, UserRole role = UserRole.User)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (_userRepo.GetByUsername(username) is not null)
            return false;

        var (hash, _) = HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = Convert.ToBase64String(hash),
            Role = role
        };
        return _userRepo.Insert(user);
    }

    public IReadOnlyList<(string Id, string Name, string? Description)> ListPermissions() =>
        _permissionRepo.ListPermissions();

    public IReadOnlyList<(string Id, string Name, IReadOnlyList<(string PermissionId, int State)> Permissions)> ListRoles() =>
        _permissionRepo.ListRoles();

    public (IReadOnlyList<(string PermissionId, int State)> Permissions, IReadOnlyList<string> RoleIds) GetUserPermissions(Guid userId) =>
        _permissionRepo.GetUserPermissions(userId);

    public bool SetUserPermission(Guid userId, string permissionId, int? state) =>
        _permissionRepo.SetUserPermission(userId, permissionId, state);

    public bool SetUserRole(Guid userId, string roleId, bool assign) =>
        _permissionRepo.SetUserRole(userId, roleId, assign);

    public (IReadOnlyList<(string RoleId, int State)> RoleStates, IReadOnlyList<(Guid UserId, int State)> UserStates) GetChannelPermissions(Guid channelId) =>
        _permissionRepo.GetChannelPermissions(channelId);

    public bool SetChannelRolePermission(Guid channelId, string roleId, int? state) =>
        _permissionRepo.SetChannelRolePermission(channelId, roleId, state);

    public bool SetChannelUserPermission(Guid channelId, Guid userId, int? state) =>
        _permissionRepo.SetChannelUserPermission(channelId, userId, state);

    private static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        var combined = new byte[SaltSize + HashSize];
        salt.CopyTo(combined, 0);
        hash.CopyTo(combined, SaltSize);
        return (combined, salt);
    }

    private static bool VerifyPassword(string password, string storedHashBase64)
    {
        var combined = Convert.FromBase64String(storedHashBase64);
        if (combined.Length != SaltSize + HashSize)
            return false;

        var salt = combined.AsSpan(0, SaltSize);
        var storedHash = combined.AsSpan(SaltSize, HashSize);

        var computedHash = Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(password),
            salt.ToArray(),
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }
}
