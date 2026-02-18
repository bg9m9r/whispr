using Microsoft.EntityFrameworkCore;

namespace Whispr.Server.Data;

/// <summary>
/// Entity Framework DbContext for Whispr server data.
/// </summary>
public sealed class WhisprDbContext(DbContextOptions<WhisprDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<UserPermissionEntity> UserPermissions => Set<UserPermissionEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();
    public DbSet<ChannelRolePermissionEntity> ChannelRolePermissions => Set<ChannelRolePermissionEntity>();
    public DbSet<ChannelUserPermissionEntity> ChannelUserPermissions => Set<ChannelUserPermissionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChannelEntity>()
            .Property(c => c.IsDefault)
            .HasConversion<int>();

        modelBuilder.Entity<RolePermissionEntity>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });
        modelBuilder.Entity<UserPermissionEntity>()
            .HasKey(up => new { up.UserId, up.PermissionId });
        modelBuilder.Entity<UserRoleEntity>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<ChannelRolePermissionEntity>()
            .HasKey(crp => new { crp.ChannelId, crp.RoleId, crp.PermissionId });
        modelBuilder.Entity<ChannelUserPermissionEntity>()
            .HasKey(cup => new { cup.ChannelId, cup.UserId, cup.PermissionId });
    }
}
