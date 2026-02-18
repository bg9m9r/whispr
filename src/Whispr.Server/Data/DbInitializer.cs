using Microsoft.EntityFrameworkCore;

namespace Whispr.Server.Data;

/// <summary>
/// Ensures database schema exists and seeds default permissions/roles/channels.
/// </summary>
public static class DbInitializer
{
    public static void Initialize(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return;

        var path = Path.GetFullPath(dbPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connectionString = $"Data Source={path}";

        var options = new DbContextOptionsBuilder<WhisprDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var ctx = new WhisprDbContext(options);
        ctx.Database.EnsureCreated();
        SeedPermissionsAndRoles(ctx);
        SeedDefaultChannel(ctx);
        MigrateFromJsonIfNeeded(ctx, path);
        SeedAdminUserRoles(ctx);
    }

    private static void MigrateFromJsonIfNeeded(WhisprDbContext ctx, string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath) ?? ".";
        var jsonPath = Path.Combine(dir, "users.json");
        if (!File.Exists(jsonPath))
            return;
        if (ctx.Users.Any())
            return;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Array)
                return;

            foreach (var el in root.EnumerateArray())
            {
                if (!el.TryGetProperty("id", out var idEl) || !Guid.TryParse(idEl.GetString(), out var id))
                    continue;
                if (!el.TryGetProperty("username", out var uEl))
                    continue;
                var username = uEl.GetString();
                if (string.IsNullOrWhiteSpace(username))
                    continue;
                if (!el.TryGetProperty("passwordHash", out var hEl))
                    continue;
                var hash = hEl.GetString();
                if (string.IsNullOrWhiteSpace(hash))
                    continue;
                var role = 0;
                if (el.TryGetProperty("role", out var rEl) && rEl.TryGetInt32(out var r))
                    role = r;

                ctx.Users.Add(new UserEntity
                {
                    Id = id.ToString(),
                    Username = username,
                    PasswordHash = hash,
                    Role = role
                });
            }
            ctx.SaveChanges();
        }
        catch
        {
            // ignore migration errors
        }
    }

    private static void SeedPermissionsAndRoles(WhisprDbContext ctx)
    {
        if (ctx.Permissions.Any())
            return;

        ctx.Permissions.AddRange(
            new PermissionEntity { Id = "admin", Name = "Admin", Description = "Server administrator" },
            new PermissionEntity { Id = "channel_access", Name = "Channel access", Description = "Access to join and view a channel" });
        ctx.SaveChanges();

        ctx.Roles.Add(new RoleEntity { Id = "admin", Name = "Admin" });
        ctx.SaveChanges();

        ctx.RolePermissions.Add(new RolePermissionEntity { RoleId = "admin", PermissionId = "admin", State = 0 });
        ctx.SaveChanges();
    }

    private static void SeedDefaultChannel(WhisprDbContext ctx)
    {
        if (ctx.Channels.Any())
            return;

        var id = Guid.NewGuid();
        var keyMaterial = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        ctx.Channels.Add(new ChannelEntity
        {
            Id = id.ToString(),
            Name = "General",
            KeyMaterial = keyMaterial,
            IsDefault = true
        });
        ctx.SaveChanges();
    }

    private static void SeedAdminUserRoles(WhisprDbContext ctx)
    {
        var adminUsers = ctx.Users.Where(u => u.Role == 1).Select(u => u.Id).ToList();
        foreach (var uid in adminUsers)
        {
            if (!ctx.UserRoles.Any(ur => ur.UserId == uid && ur.RoleId == "admin"))
            {
                ctx.UserRoles.Add(new UserRoleEntity { UserId = uid, RoleId = "admin" });
            }
        }
        ctx.SaveChanges();
    }
}
