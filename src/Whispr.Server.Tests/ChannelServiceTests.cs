using Whispr.Server.Repositories;
using Whispr.Server.Server;
using Whispr.Server.Services;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class ChannelServiceTests
{
    private static IChannelService CreateChannelService()
    {
        var repo = new InMemoryChannelRepository();
        return new ChannelManager(repo);
    }

    [Fact]
    public void JoinDefaultChannel_ReturnsChannelAndKeyMaterial()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        var result = channels.JoinDefaultChannel(userId);
        Assert.NotNull(result);
        Assert.NotNull(result.Value.Channel);
        Assert.NotEmpty(result.Value.KeyMaterial);
        Assert.Equal("General", result.Value.Channel.Name);
    }

    [Fact]
    public void JoinDefaultChannel_ThenLeave_CanRejoin()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        var join1 = channels.JoinDefaultChannel(userId);
        Assert.NotNull(join1);

        var leave = channels.LeaveChannel(userId);
        Assert.NotNull(leave);

        var join2 = channels.JoinDefaultChannel(userId);
        Assert.NotNull(join2);
    }

    [Fact]
    public void CreateChannel_ReturnsNewChannel()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        channels.JoinDefaultChannel(userId);

        var channel = channels.CreateChannel("Voice Chat", userId);
        Assert.NotNull(channel);
        Assert.Equal("Voice Chat", channel.Name);
        Assert.NotEqual(Guid.Empty, channel.Id);
    }

    [Fact]
    public void CreateChannel_ThenJoinChannel_Succeeds()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        channels.JoinDefaultChannel(userId);

        var created = channels.CreateChannel("New Room", userId);
        Assert.NotNull(created);

        var joinResult = channels.JoinChannel(created.Id, userId);
        Assert.NotNull(joinResult);
        Assert.Equal(created.Id, joinResult.Value.Channel.Id);
    }

    [Fact]
    public void LeaveChannel_WhenNotInChannel_ReturnsNull()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        var result = channels.LeaveChannel(userId);
        Assert.Null(result);
    }

    [Fact]
    public void LeaveChannel_WhenInChannel_ReturnsRemainingMembers()
    {
        var channels = CreateChannelService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        channels.JoinDefaultChannel(user1);
        channels.JoinDefaultChannel(user2);

        var leave = channels.LeaveChannel(user1);
        Assert.NotNull(leave);
        Assert.Single(leave.Value.RemainingMembers);
        Assert.Equal(user2, leave.Value.RemainingMembers[0]);
    }

    [Fact]
    public void GetUserChannel_WhenInChannel_ReturnsChannelId()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        var join = channels.JoinDefaultChannel(userId);
        Assert.NotNull(join);

        var channelId = channels.GetUserChannel(userId);
        Assert.NotNull(channelId);
        Assert.Equal(join.Value.Channel.Id, channelId);
    }

    [Fact]
    public void GetUserChannel_WhenNotInChannel_ReturnsNull()
    {
        var channels = CreateChannelService();
        Assert.Null(channels.GetUserChannel(Guid.NewGuid()));
    }

    [Fact]
    public void ListChannels_IncludesDefaultChannel()
    {
        var channels = CreateChannelService();
        var list = channels.ListChannels();
        Assert.NotEmpty(list);
        Assert.Contains(list, c => c.Name == "General");
    }

    [Fact]
    public void GetOtherMembers_ExcludesSpecifiedUser()
    {
        var channels = CreateChannelService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        channels.JoinDefaultChannel(user1);
        channels.JoinDefaultChannel(user2);

        var others = channels.GetOtherMembers(channels.GetUserChannel(user1)!.Value, user1);
        Assert.NotNull(others);
        Assert.Single(others);
        Assert.Equal(user2, others[0]);
    }

    [Fact]
    public void CanCreateMoreChannels_WhenUnderLimit_ReturnsTrue()
    {
        var channels = CreateChannelService();
        Assert.True(channels.CanCreateMoreChannels);
    }

    [Fact]
    public void CreateChannel_UpToMaxChannels_ThenFails()
    {
        var repo = new InMemoryChannelRepository();
        var channels = new ChannelManager(repo);
        var userId = Guid.NewGuid();
        channels.JoinDefaultChannel(userId);

        for (var i = 0; i < 9; i++)
            Assert.NotNull(channels.CreateChannel($"Channel {i}", userId));

        Assert.Null(channels.CreateChannel("OneTooMany", userId));
        Assert.False(channels.CanCreateMoreChannels);
    }

    [Fact]
    public void JoinChannel_NonExistent_ReturnsNull()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        channels.JoinDefaultChannel(userId);
        var result = channels.JoinChannel(Guid.NewGuid(), userId);
        Assert.Null(result);
    }

    [Fact]
    public void GetChannelKeyMaterial_ReturnsCorrectKey()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        var join = channels.JoinDefaultChannel(userId);
        Assert.NotNull(join);
        var channelId = join.Value.Channel.Id;
        var expectedKey = join.Value.KeyMaterial;
        var actualKey = channels.GetChannelKeyMaterial(channelId);
        Assert.NotNull(actualKey);
        Assert.Equal(expectedKey, actualKey);
    }

    [Fact]
    public void CreateChannel_EmptyName_Throws()
    {
        var channels = CreateChannelService();
        var userId = Guid.NewGuid();
        channels.JoinDefaultChannel(userId);
        Assert.Throws<ArgumentException>(() => channels.CreateChannel("", userId));
        Assert.Throws<ArgumentException>(() => channels.CreateChannel("   ", userId));
    }
}
