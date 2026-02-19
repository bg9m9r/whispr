using Whispr.Core.Models;
using Whispr.Server.Repositories;
using Whispr.Server.Services;
using Whispr.Server.Tests.Fakes;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class AuthServicePermissionTests
{
    private static AuthService CreateAuthServiceWithFakePermissions(FakePermissionRepository fakePerm)
    {
        var userRepo = new InMemoryUserRepository();
        return new AuthService(userRepo, fakePerm, new ServerOptions { SeedTestUsers = true });
    }

    [Fact]
    public void HasPermission_AdminRole_GrantsAdminPermission()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("admin", "Admin");
        fakePerm.AddRole("admin", "Admin");
        fakePerm.SetRolePermission("admin", "admin", 0);

        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        fakePerm.SetUserRole(user.Id, "admin", assign: true);
        Assert.True(auth.HasPermission(user.Id, "admin"));
    }

    [Fact]
    public void HasPermission_DirectAllow_GrantsPermission()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("manage_users", "Manage Users");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        fakePerm.SetUserPermission(user.Id, "manage_users", 0);
        Assert.True(auth.HasPermission(user.Id, "manage_users"));
    }

    [Fact]
    public void HasPermission_DirectDeny_RevokesPermission()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("manage_users", "Manage Users");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        fakePerm.SetUserPermission(user.Id, "manage_users", 1);
        Assert.False(auth.HasPermission(user.Id, "manage_users"));
    }

    [Fact]
    public void HasPermission_RoleWithAllow_GrantsPermission()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("manage_users", "Manage Users");
        fakePerm.AddRole("moderator", "Moderator");
        fakePerm.SetRolePermission("moderator", "manage_users", 0);

        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        fakePerm.SetUserRole(user.Id, "moderator", assign: true);
        Assert.True(auth.HasPermission(user.Id, "manage_users"));
    }

    [Fact]
    public void HasPermission_NoPermission_ReturnsFalse()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("manage_users", "Manage Users");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        Assert.False(auth.HasPermission(user.Id, "manage_users"));
    }

    [Fact]
    public void CanAccessChannel_NoPermissions_PublicChannel_ReturnsTrue()
    {
        var fakePerm = new FakePermissionRepository();
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        var channelId = Guid.NewGuid();
        Assert.True(auth.CanAccessChannel(user.Id, channelId));
    }

    [Fact]
    public void CanAccessChannel_UserDenied_ReturnsFalse()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("channel_access", "Channel Access");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        var channelId = Guid.NewGuid();
        fakePerm.SetChannelUserPermission(channelId, user.Id, 1);
        Assert.False(auth.CanAccessChannel(user.Id, channelId));
    }

    [Fact]
    public void CanAccessChannel_UserAllowed_ReturnsTrue()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("channel_access", "Channel Access");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        var channelId = Guid.NewGuid();
        fakePerm.SetChannelUserPermission(channelId, user.Id, 0);
        Assert.True(auth.CanAccessChannel(user.Id, channelId));
    }

    [Fact]
    public void CanAccessChannel_Admin_BypassesRestrictions()
    {
        var fakePerm = new FakePermissionRepository();
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        var admin = auth.ValidateCredentials("admin", "admin")!;
        var channelId = Guid.NewGuid();
        fakePerm.SetChannelUserPermission(channelId, admin.Id, 1);
        Assert.True(auth.CanAccessChannel(admin.Id, channelId));
    }

    [Fact]
    public void ListPermissions_ReturnsFromRepository()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("admin", "Admin", "Server admin");
        fakePerm.AddPermission("channel_access", "Channel Access", "Join channels");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        var perms = auth.ListPermissions();
        Assert.Equal(2, perms.Count);
        Assert.Contains(perms, p => p.Id == "admin" && p.Name == "Admin");
        Assert.Contains(perms, p => p.Id == "channel_access" && p.Name == "Channel Access");
    }

    [Fact]
    public void ListRoles_ReturnsFromRepository()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("admin", "Admin");
        fakePerm.AddRole("admin", "Admin");
        fakePerm.SetRolePermission("admin", "admin", 0);
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        var roles = auth.ListRoles();
        Assert.Single(roles);
        Assert.Equal("admin", roles[0].Id);
        Assert.Equal("Admin", roles[0].Name);
        Assert.Contains(roles[0].Permissions, p => p.PermissionId == "admin" && p.State == 0);
    }

    [Fact]
    public void GetUserPermissions_ReturnsDirectAndRoles()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("admin", "Admin");
        fakePerm.AddPermission("manage_users", "Manage Users");
        fakePerm.AddRole("moderator", "Moderator");
        fakePerm.SetRolePermission("moderator", "manage_users", 0);

        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        fakePerm.SetUserPermission(user.Id, "admin", 0);
        fakePerm.SetUserRole(user.Id, "moderator", assign: true);

        var (perms, roleIds) = auth.GetUserPermissions(user.Id);
        Assert.Contains(perms, p => p.PermissionId == "admin" && p.State == 0);
        Assert.Contains(roleIds, r => r == "moderator");
    }

    [Fact]
    public void SetUserPermission_AddAndRemove()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("manage_users", "Manage Users");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;

        auth.SetUserPermission(user.Id, "manage_users", 0);
        Assert.True(auth.HasPermission(user.Id, "manage_users"));

        auth.SetUserPermission(user.Id, "manage_users", null);
        Assert.False(auth.HasPermission(user.Id, "manage_users"));
    }

    [Fact]
    public void SetUserRole_AssignAndRemove()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("manage_users", "Manage Users");
        fakePerm.AddRole("moderator", "Moderator");
        fakePerm.SetRolePermission("moderator", "manage_users", 0);

        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;

        auth.SetUserRole(user.Id, "moderator", assign: true);
        Assert.True(auth.HasPermission(user.Id, "manage_users"));

        auth.SetUserRole(user.Id, "moderator", assign: false);
        Assert.False(auth.HasPermission(user.Id, "manage_users"));
    }

    [Fact]
    public void GetChannelPermissions_ReturnsRoleAndUserStates()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("channel_access", "Channel Access");
        fakePerm.AddRole("member", "Member");
        fakePerm.SetRolePermission("member", "channel_access", 0);

        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        var channelId = Guid.NewGuid();

        fakePerm.SetChannelRolePermission(channelId, "member", 0);
        fakePerm.SetChannelUserPermission(channelId, user.Id, 0);

        var (roleStates, userStates) = auth.GetChannelPermissions(channelId);
        Assert.Contains(roleStates, r => r.RoleId == "member" && r.State == 0);
        Assert.Contains(userStates, u => u.UserId == user.Id && u.State == 0);
    }

    [Fact]
    public void SetChannelRolePermission_AddAndRemove()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("channel_access", "Channel Access");
        fakePerm.AddRole("member", "Member");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        var channelId = Guid.NewGuid();

        auth.SetChannelRolePermission(channelId, "member", 0);
        var (roleStates, _) = auth.GetChannelPermissions(channelId);
        Assert.Contains(roleStates, r => r.RoleId == "member");

        auth.SetChannelRolePermission(channelId, "member", null);
        var (afterRemove, _) = auth.GetChannelPermissions(channelId);
        Assert.DoesNotContain(afterRemove, r => r.RoleId == "member");
    }

    [Fact]
    public void SetChannelUserPermission_AddAndRemove()
    {
        var fakePerm = new FakePermissionRepository();
        fakePerm.AddPermission("channel_access", "Channel Access");
        var auth = CreateAuthServiceWithFakePermissions(fakePerm);
        Assert.True(auth.AddUser("alice", "pass", UserRole.User));
        var user = auth.ValidateCredentials("alice", "pass")!;
        var channelId = Guid.NewGuid();

        auth.SetChannelUserPermission(channelId, user.Id, 0);
        var (_, userStatesBefore) = auth.GetChannelPermissions(channelId);
        Assert.Contains(userStatesBefore, u => u.UserId == user.Id);

        auth.SetChannelUserPermission(channelId, user.Id, null);
        var (_, userStatesAfter) = auth.GetChannelPermissions(channelId);
        Assert.DoesNotContain(userStatesAfter, u => u.UserId == user.Id);
    }
}
