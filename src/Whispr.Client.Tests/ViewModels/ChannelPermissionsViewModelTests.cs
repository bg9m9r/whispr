using Whispr.Client.Services;
using Whispr.Client.Tests.Fakes;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;
using Xunit;

namespace Whispr.Client.Tests.ViewModels;

public sealed class ChannelPermissionsViewModelTests
{
    [Fact]
    public async Task Load_PopulatesRolesAndUsers()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channelService = new FakeChannelService();
        channelService.SetRoles(new RolesListPayload
        {
            Roles = [new RoleInfo { Id = "member", Name = "Member", Permissions = [] }]
        });
        channelService.SetChannelPermissions(channelId, new ChannelPermissionsPayload
        {
            ChannelId = channelId,
            RoleStates = [new ChannelRoleState { RoleId = "member", State = "allow" }],
            UserStates = [new ChannelUserState { UserId = userId, State = "allow" }]
        });
        channelService.ServerState = new ServerStatePayload
        {
            Channels =
            [
                new ChannelInfo
                {
                    Id = channelId,
                    Name = "General",
                    MemberIds = [userId],
                    Members = [new MemberInfo { UserId = userId, Username = "alice", ClientId = 0 }]
                }
            ],
            CanCreateChannel = true
        };
        channelService.SetUsernameForUserId(userId, "alice");

        var vm = new ChannelPermissionsViewModel(channelService, channelId, "General");

        await Task.Delay(150);

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsContentVisible);
        Assert.Single(vm.RolesList);
        Assert.Single(vm.UsersList);
    }
}
