using Whispr.Client.Services;
using Whispr.Client.Tests.Fakes;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;
using Xunit;

namespace Whispr.Client.Tests.ViewModels;

public sealed class PermissionsViewModelTests
{
    [Fact]
    public async Task Load_PopulatesPermissionsAndRoles()
    {
        var channelService = new FakeChannelService();
        channelService.SetPermissions(new PermissionsListPayload
        {
            Permissions =
            [
                new PermissionInfo { Id = "admin", Name = "Admin", Description = "Server admin" },
                new PermissionInfo { Id = "channel_access", Name = "Channel Access", Description = "Join channels" }
            ]
        });
        channelService.SetRoles(new RolesListPayload
        {
            Roles =
            [
                new RoleInfo { Id = "admin", Name = "Admin", Permissions = [new RolePermissionAssignment { PermissionId = "admin", State = "allow" }] }
            ]
        });
        var userId = Guid.NewGuid();
        channelService.SetUserPermissions(userId, new UserPermissionsPayload
        {
            UserId = userId,
            Permissions = [new UserPermissionAssignment { PermissionId = "admin", State = "allow" }],
            RoleIds = ["admin"]
        });

        var vm = new PermissionsViewModel(channelService, userId, "alice");

        await Task.Delay(150);

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsContentVisible);
        Assert.Equal(2, vm.PermissionsList.Count);
        Assert.Single(vm.RolesList);
    }

    [Fact]
    public async Task ChangingPermission_SetsIsApplyEnabled()
    {
        var channelService = new FakeChannelService();
        channelService.SetPermissions(new PermissionsListPayload { Permissions = [new PermissionInfo { Id = "admin", Name = "Admin", Description = "" }] });
        channelService.SetRoles(new RolesListPayload { Roles = [] });
        var userId = Guid.NewGuid();
        channelService.SetUserPermissions(userId, new UserPermissionsPayload { UserId = userId, Permissions = [], RoleIds = [] });

        var vm = new PermissionsViewModel(channelService, userId, "alice");

        await Task.Delay(150);

        Assert.False(vm.IsApplyEnabled);
        vm.PermissionsList[0].SelectedState = "Allow";
        Assert.True(vm.IsApplyEnabled);
    }

    [Fact]
    public async Task ChangingRole_SetsIsApplyEnabled()
    {
        var channelService = new FakeChannelService();
        channelService.SetPermissions(new PermissionsListPayload { Permissions = [] });
        channelService.SetRoles(new RolesListPayload { Roles = [new RoleInfo { Id = "moderator", Name = "Moderator", Permissions = [] }] });
        var userId = Guid.NewGuid();
        channelService.SetUserPermissions(userId, new UserPermissionsPayload { UserId = userId, Permissions = [], RoleIds = [] });

        var vm = new PermissionsViewModel(channelService, userId, "alice");

        await Task.Delay(150);

        Assert.False(vm.IsApplyEnabled);
        vm.RolesList[0].IsAssigned = true;
        Assert.True(vm.IsApplyEnabled);
    }

    [Fact]
    public async Task ApplyCommand_CallsSetUserPermissionAndSetUserRole()
    {
        var channelService = new FakeChannelService();
        channelService.SetPermissions(new PermissionsListPayload
        {
            Permissions = [new PermissionInfo { Id = "admin", Name = "Admin", Description = "" }]
        });
        channelService.SetRoles(new RolesListPayload
        {
            Roles = [new RoleInfo { Id = "moderator", Name = "Moderator", Permissions = [] }]
        });
        var userId = Guid.NewGuid();
        channelService.SetUserPermissions(userId, new UserPermissionsPayload { UserId = userId, Permissions = [], RoleIds = [] });

        var vm = new PermissionsViewModel(channelService, userId, "alice");

        await Task.Delay(150);

        vm.PermissionsList[0].SelectedState = "Allow";
        vm.RolesList[0].IsAssigned = true;
        Assert.True(vm.IsApplyEnabled);

        await vm.ApplyCommand.ExecuteAsync(null!);

        Assert.False(vm.HasError);
        Assert.False(vm.IsApplyEnabled);
        Assert.Equal("Allow", vm.PermissionsList[0].SelectedState);
        Assert.True(vm.RolesList[0].IsAssigned);
    }
}
