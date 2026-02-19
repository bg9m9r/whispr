using Whispr.Core.Models;
using Whispr.Server.Repositories;
using Whispr.Server.Services;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class AuthServiceTests
{
    private static AuthService CreateAuthService()
    {
        var userRepo = new InMemoryUserRepository();
        var permRepo = new InMemoryPermissionRepository();
        return new AuthService(userRepo, permRepo, new ServerOptions { SeedTestUsers = true });
    }

    [Fact]
    public void ValidateCredentials_WithSeededAdmin_ReturnsUser()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("admin", "admin");
        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void ValidateCredentials_WithWrongPassword_ReturnsNull()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("admin", "wrong");
        Assert.Null(user);
    }

    [Fact]
    public void ValidateCredentials_WithNonExistentUser_ReturnsNull()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("nonexistent", "any");
        Assert.Null(user);
    }

    [Fact]
    public void AddUser_NewUser_ReturnsTrue()
    {
        var auth = CreateAuthService();
        var result = auth.AddUser("alice", "secret123", UserRole.User);
        Assert.True(result);
        var user = auth.ValidateCredentials("alice", "secret123");
        Assert.NotNull(user);
        Assert.Equal("alice", user.Username);
    }

    [Fact]
    public void AddUser_DuplicateUsername_ReturnsFalse()
    {
        var auth = CreateAuthService();
        Assert.True(auth.AddUser("alice", "secret", UserRole.User));
        Assert.False(auth.AddUser("alice", "other", UserRole.User));
    }

    [Fact]
    public void IssueSessionToken_AndValidateToken_ReturnsUser()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("admin", "admin")!;
        var token = auth.IssueSessionToken(user);
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var validated = auth.ValidateToken(token);
        Assert.NotNull(validated);
        Assert.Equal(user.Id, validated!.Id);
    }

    [Fact]
    public void RevokeToken_InvalidatesSession()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("admin", "admin")!;
        var token = auth.IssueSessionToken(user);
        auth.RevokeToken(token);
        Assert.Null(auth.ValidateToken(token));
    }

    [Fact]
    public void IsAdmin_AdminUser_ReturnsTrue()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("admin", "admin")!;
        Assert.True(auth.IsAdmin(user.Id));
    }

    [Fact]
    public void IsAdmin_RegularUser_ReturnsFalse()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("bob", "bob")!;
        Assert.False(auth.IsAdmin(user.Id));
    }

    [Fact]
    public void GetUsername_ExistingUser_ReturnsUsername()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateCredentials("admin", "admin")!;
        Assert.Equal("admin", auth.GetUsername(user.Id));
    }

    [Fact]
    public void GetUsername_NonExistentUser_ReturnsNull()
    {
        var auth = CreateAuthService();
        Assert.Null(auth.GetUsername(Guid.NewGuid()));
    }

    [Fact]
    public void ValidateOrRegister_ExistingUserWithCorrectPassword_ReturnsUser()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateOrRegister("admin", "admin");
        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
    }

    [Fact]
    public void ValidateOrRegister_NewUser_RegistersAndReturnsUser()
    {
        var auth = CreateAuthService();
        var user = auth.ValidateOrRegister("charlie", "pass123");
        Assert.NotNull(user);
        Assert.Equal("charlie", user.Username);
        Assert.NotNull(auth.ValidateCredentials("charlie", "pass123"));
    }
}
