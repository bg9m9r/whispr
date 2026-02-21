using Microsoft.EntityFrameworkCore;

namespace Whispr.Server.Data;

/// <summary>
/// Applies EF Core migrations at startup and seeds default permissions/roles/channels.
/// Users can run the server without any EF knowledge; schema is created/updated automatically.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Migration id of the initial migration (full schema). Used to baseline existing DBs
    /// that were created with EnsureCreated and have no __EFMigrationsHistory.
    /// </summary>
    private const string InitialMigrationId = "20260221152520_AddMessageUpdatedAt";

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

        // Baseline existing DBs that were created with EnsureCreated (no migrations history)
        BaselineExistingDatabaseIfNeeded(ctx);

        // Apply pending migrations; creates DB and schema on first run
        ctx.Database.Migrate();

        SeedPermissionsAndRoles(ctx);
        SeedDefaultChannel(ctx);
        MigrateFromJsonIfNeeded(ctx, path);
        SeedAdminUserRoles(ctx);
    }

    /// <summary>
    /// If the database has tables but no __EFMigrationsHistory (e.g. created with EnsureCreated),
    /// create the history table and record the initial migration so Migrate() won't re-apply it.
    /// </summary>
    private static void BaselineExistingDatabaseIfNeeded(WhisprDbContext ctx)
    {
        try
        {
            var hasHistoryTable = ctx.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'").FirstOrDefault() > 0;
            if (hasHistoryTable)
            {
                var hasAny = ctx.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM __EFMigrationsHistory").FirstOrDefault() > 0;
                if (hasAny)
                    return; // Already using migrations
            }

            // Check for existing tables from old EnsureCreated
            var tableCount = ctx.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('Messages','Users','Channels')").FirstOrDefault();
            if (tableCount == 0)
                return; // New DB; Migrate() will create everything

            // Old DB with no migrations history: baseline so we don't re-run the initial migration
            ctx.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                    MigrationId TEXT NOT NULL PRIMARY KEY,
                    ProductVersion TEXT NOT NULL
                )
                """);
            ctx.Database.ExecuteSqlRaw(
                "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                InitialMigrationId,
                "9.0.0");
        }
        catch
        {
            // If anything fails, Migrate() will run and may fail on duplicate table; that's acceptable
        }
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
        var hasAny = ctx.Channels.Any();
        var hasText = ctx.Channels.Any(c => c.ChannelType == 1);

        if (!hasAny)
        {
            // Brand-new DB: create default voice and text channels
            var voiceId = Guid.NewGuid();
            ctx.Channels.Add(new ChannelEntity
            {
                Id = voiceId.ToString(),
                Name = "General",
                KeyMaterial = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),
                IsDefault = true,
                ChannelType = 0 // Voice
            });
            ctx.Channels.Add(new ChannelEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Chat",
                KeyMaterial = Array.Empty<byte>(),
                IsDefault = false,
                ChannelType = 1 // Text
            });
        }
        else if (!hasText)
        {
            // Existing DB with no text channel: add default text channel
            ctx.Channels.Add(new ChannelEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Chat",
                KeyMaterial = Array.Empty<byte>(),
                IsDefault = false,
                ChannelType = 1 // Text
            });
        }

        if (!hasAny || !hasText)
            ctx.SaveChanges();
    }

    private static void SeedAdminUserRoles(WhisprDbContext ctx)
    {
        var adminUsers = ctx.Users.Where(u => u.Role == 1).Select(u => u.Id).ToList();
        foreach (var uid in adminUsers.Where(uid => !ctx.UserRoles.Any(ur => ur.UserId == uid && ur.RoleId == "admin")))
        {
            ctx.UserRoles.Add(new UserRoleEntity { UserId = uid, RoleId = "admin" });
        }
        ctx.SaveChanges();
    }
}
