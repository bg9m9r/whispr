using Microsoft.EntityFrameworkCore;
using Whispr.Core.Models;
using Whispr.Server.Repositories;

namespace Whispr.Server.Data;

/// <summary>
/// Entity Framework-backed user store implementing IUserRepository and IPermissionRepository.
/// </summary>
public sealed class EfUserStore(IDbContextFactory<WhisprDbContext> contextFactory)
    : IUserRepository, IPermissionRepository
{
    private const string ChannelAccessPermissionId = "channel_access";

    public IReadOnlyList<User> LoadAll()
    {
        using var ctx = contextFactory.CreateDbContext();
        return ctx.Users
            .AsNoTracking()
            .Select(u => new User
            {
                Id = Guid.Parse(u.Id),
                Username = u.Username,
                PasswordHash = u.PasswordHash,
                Role = (UserRole)u.Role
            })
            .ToList();
    }

    public User? GetByUsername(string username)
    {
        using var ctx = contextFactory.CreateDbContext();
        var entity = ctx.Users.AsNoTracking().FirstOrDefault(u => u.Username == username);
        return entity is null ? null : ToUser(entity);
    }

    public User? GetById(Guid id)
    {
        using var ctx = contextFactory.CreateDbContext();
        var entity = ctx.Users.AsNoTracking().FirstOrDefault(u => u.Id == id.ToString());
        return entity is null ? null : ToUser(entity);
    }

    public bool Insert(User user)
    {
        using var ctx = contextFactory.CreateDbContext();
        ctx.Users.Add(new UserEntity
        {
            Id = user.Id.ToString(),
            Username = user.Username,
            PasswordHash = user.PasswordHash ?? "",
            Role = (int)user.Role
        });
        try
        {
            ctx.SaveChanges();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public IReadOnlyList<(string Id, string Name, string? Description)> ListPermissions()
    {
        using var ctx = contextFactory.CreateDbContext();
        return ctx.Permissions.AsNoTracking()
            .ToList()
            .Select(p => (p.Id, p.Name, p.Description))
            .ToList();
    }

    public IReadOnlyList<(string Id, string Name, IReadOnlyList<(string PermissionId, int State)> Permissions)> ListRoles()
    {
        using var ctx = contextFactory.CreateDbContext();
        var roles = ctx.Roles.AsNoTracking().ToList();
        var rolePerms = ctx.RolePermissions.AsNoTracking().ToList();
        return roles.Select(r => (
            r.Id,
            r.Name,
            (IReadOnlyList<(string, int)>)rolePerms
                .Where(rp => rp.RoleId == r.Id)
                .Select(rp => (rp.PermissionId, rp.State))
                .ToList()
        )).ToList();
    }

    public (IReadOnlyList<(string PermissionId, int State)> Permissions, IReadOnlyList<string> RoleIds) GetUserPermissions(Guid userId)
    {
        var uid = userId.ToString();
        using var ctx = contextFactory.CreateDbContext();
        var perms = ctx.UserPermissions.AsNoTracking()
            .Where(up => up.UserId == uid)
            .ToList()
            .Select(up => (up.PermissionId, up.State))
            .ToList();
        var roles = ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == uid)
            .Select(ur => ur.RoleId)
            .ToList();
        return (perms, roles);
    }

    public bool SetUserPermission(Guid userId, string permissionId, int? state)
    {
        var uid = userId.ToString();
        using var ctx = contextFactory.CreateDbContext();
        var existing = ctx.UserPermissions.Find(uid, permissionId);
        if (state is null)
        {
            if (existing is null) return true;
            ctx.UserPermissions.Remove(existing);
        }
        else
        {
            if (existing is not null)
                existing.State = state.Value;
            else
                ctx.UserPermissions.Add(new UserPermissionEntity { UserId = uid, PermissionId = permissionId, State = state.Value });
        }

        ctx.SaveChanges();
        return true;
    }

    public bool SetUserRole(Guid userId, string roleId, bool assign)
    {
        var uid = userId.ToString();
        using var ctx = contextFactory.CreateDbContext();
        if (assign)
        {
            if (ctx.UserRoles.Find(uid, roleId) is not null) return true;
            ctx.UserRoles.Add(new UserRoleEntity { UserId = uid, RoleId = roleId });
        }
        else
        {
            var existing = ctx.UserRoles.Find(uid, roleId);
            if (existing is null) return true;
            ctx.UserRoles.Remove(existing);
        }

        ctx.SaveChanges();
        return true;
    }

    public bool CanAccessChannel(Guid userId, Guid channelId, bool isAdmin)
    {
        if (isAdmin) return true;

        var cid = channelId.ToString();
        var uid = userId.ToString();

        using var ctx = contextFactory.CreateDbContext();
        var hasAny = ctx.ChannelRolePermissions.Any(crp => crp.ChannelId == cid) ||
                     ctx.ChannelUserPermissions.Any(cup => cup.ChannelId == cid);
        if (!hasAny) return true;

        var states = new List<int>();
        states.AddRange(ctx.ChannelUserPermissions
            .Where(cup => cup.ChannelId == cid && cup.UserId == uid && cup.PermissionId == ChannelAccessPermissionId)
            .Select(cup => cup.State));

        var (_, roleIds) = GetUserPermissions(userId);
        foreach (var roleId in roleIds)
        {
            states.AddRange(ctx.ChannelRolePermissions
                .Where(crp => crp.ChannelId == cid && crp.RoleId == roleId && crp.PermissionId == ChannelAccessPermissionId)
                .Select(crp => crp.State));
        }

        if (states.Count == 0) return true;
        return !states.Contains(1) && states.Contains(0);
    }

    public (IReadOnlyList<(string RoleId, int State)> RoleStates, IReadOnlyList<(Guid UserId, int State)> UserStates) GetChannelPermissions(Guid channelId)
    {
        var cid = channelId.ToString();
        using var ctx = contextFactory.CreateDbContext();
        var roleStates = ctx.ChannelRolePermissions
            .Where(crp => crp.ChannelId == cid && crp.PermissionId == ChannelAccessPermissionId)
            .ToList()
            .Select(crp => (crp.RoleId, crp.State))
            .ToList();
        var userStates = ctx.ChannelUserPermissions
            .Where(cup => cup.ChannelId == cid && cup.PermissionId == ChannelAccessPermissionId)
            .ToList()
            .Select(cup => (Guid.Parse(cup.UserId), cup.State))
            .ToList();
        return (roleStates, userStates);
    }

    public bool SetChannelRolePermission(Guid channelId, string roleId, int? state)
    {
        var cid = channelId.ToString();
        using var ctx = contextFactory.CreateDbContext();
        var existing = ctx.ChannelRolePermissions.Find(cid, roleId, ChannelAccessPermissionId);
        if (state is null)
        {
            if (existing is null) return true;
            ctx.ChannelRolePermissions.Remove(existing);
        }
        else
        {
            if (existing is not null)
                existing.State = state.Value;
            else
                ctx.ChannelRolePermissions.Add(new ChannelRolePermissionEntity
                {
                    ChannelId = cid,
                    RoleId = roleId,
                    PermissionId = ChannelAccessPermissionId,
                    State = state.Value
                });
        }

        ctx.SaveChanges();
        return true;
    }

    public bool SetChannelUserPermission(Guid channelId, Guid userId, int? state)
    {
        var cid = channelId.ToString();
        var uid = userId.ToString();
        using var ctx = contextFactory.CreateDbContext();
        var existing = ctx.ChannelUserPermissions.Find(cid, uid, ChannelAccessPermissionId);
        if (state is null)
        {
            if (existing is null) return true;
            ctx.ChannelUserPermissions.Remove(existing);
        }
        else
        {
            if (existing is not null)
                existing.State = state.Value;
            else
                ctx.ChannelUserPermissions.Add(new ChannelUserPermissionEntity
                {
                    ChannelId = cid,
                    UserId = uid,
                    PermissionId = ChannelAccessPermissionId,
                    State = state.Value
                });
        }

        ctx.SaveChanges();
        return true;
    }

    private static User ToUser(UserEntity e) => new()
    {
        Id = Guid.Parse(e.Id),
        Username = e.Username,
        PasswordHash = e.PasswordHash,
        Role = (UserRole)e.Role
    };
}
