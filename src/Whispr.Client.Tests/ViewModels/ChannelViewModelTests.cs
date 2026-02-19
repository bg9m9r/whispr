using Whispr.Client.Models;
using Whispr.Client.Services;
using Whispr.Client.Tests.Fakes;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;
using Xunit;

namespace Whispr.Client.Tests.ViewModels;

public sealed class ChannelViewModelTests
{
    [Fact]
    public void ContextMenuTargetNode_RootNode_IsCreateChannelVisible()
    {
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(Guid.NewGuid(), "General", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        var rootNode = vm.ServerTreeRootItems[0];
        vm.ContextMenuTargetNode = rootNode;

        Assert.True(vm.IsCreateChannelVisible);
        Assert.True(vm.IsCreateChannelEnabled);
    }

    [Fact]
    public void ContextMenuTargetNode_UserNode_IsEditPermissionsVisible()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(channelId, "General", "voice", [userId], [new MemberInfo { UserId = userId, Username = "bob", ClientId = 0 }], null);
        channelService.SetChannelResult(channelResult);
        channelService.SetUsernameForUserId(userId, "bob");
        var serverState = new ServerStatePayload
        {
            Channels = [new ChannelInfo { Id = channelId, Name = "General", MemberIds = [userId], Members = [new MemberInfo { UserId = userId, Username = "bob", ClientId = 0 }] }],
            CanCreateChannel = true
        };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        var userNode = vm.ServerTreeRootItems[0].Children[0].Children[0];
        vm.ContextMenuTargetNode = userNode;

        Assert.True(vm.IsEditPermissionsVisible);
    }

    [Fact]
    public void ContextMenuTargetNode_ChannelNode_IsEditPermissionsVisible()
    {
        var channelId = Guid.NewGuid();
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(channelId, "General", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        var serverState = new ServerStatePayload { Channels = [new ChannelInfo { Id = channelId, Name = "General", MemberIds = [], Members = [] }], CanCreateChannel = true };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        var channelNode = vm.ServerTreeRootItems[0].Children[0];
        vm.ContextMenuTargetNode = channelNode;

        Assert.True(vm.IsEditPermissionsVisible);
    }

    [Fact]
    public void ContextMenuTargetNode_Null_IsCreateChannelVisibleFalse()
    {
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(Guid.NewGuid(), "General", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        vm.ContextMenuTargetNode = null;

        Assert.False(vm.IsCreateChannelVisible);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(Guid.NewGuid(), "General", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        vm.Dispose();
    }

    [Fact]
    public async Task SwitchChannelCommand_UpdatesCurrentChannelName()
    {
        var channelId = Guid.NewGuid();
        var otherChannelId = Guid.NewGuid();
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(channelId, "General", "voice", [], [], null);
        var newChannelResult = new ChannelJoinedResult(otherChannelId, "Voice Chat", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        channelService.ServerState = new ServerStatePayload
        {
            Channels =
            [
                new ChannelInfo { Id = channelId, Name = "General", MemberIds = [], Members = [] },
                new ChannelInfo { Id = otherChannelId, Name = "Voice Chat", MemberIds = [], Members = [] }
            ],
            CanCreateChannel = true
        };
        channelService.Start(channelResult, channelService.ServerState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, channelService.ServerState, "localhost");

        channelService.SetChannelResult(newChannelResult);

        await vm.SwitchChannelCommand.ExecuteAsync(otherChannelId);

        Assert.Equal("Voice Chat", vm.CurrentChannelName);
    }

    [Fact]
    public async Task CreateChannelCommand_EmptyName_DoesNotCreate()
    {
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(Guid.NewGuid(), "General", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        vm.CreateChannelName = "";
        await vm.CreateChannelCommand.ExecuteAsync(null!);

        Assert.Equal("General", vm.CurrentChannelName);
    }

    [Fact]
    public async Task CreateChannelCommand_WithName_UpdatesCurrentChannelName()
    {
        var channelId = Guid.NewGuid();
        var newChannelResult = new ChannelJoinedResult(channelId, "New Room", "voice", [], [], null);
        var channelService = new FakeChannelService();
        var auth = new FakeAuthService();
        auth.SetLoggedInUser(Guid.NewGuid(), "alice");
        var channelResult = new ChannelJoinedResult(Guid.NewGuid(), "General", "voice", [], [], null);
        channelService.SetChannelResult(channelResult);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        channelService.Start(channelResult, serverState);

        var host = new FakeChannelViewHost();
        var vm = new ChannelViewModel(channelService, auth, host, channelResult, serverState, "localhost");

        channelService.SetChannelResult(newChannelResult);
        vm.CreateChannelName = "New Room";
        await vm.CreateChannelCommand.ExecuteAsync(null!);

        Assert.Equal("New Room", vm.CurrentChannelName);
    }
}
