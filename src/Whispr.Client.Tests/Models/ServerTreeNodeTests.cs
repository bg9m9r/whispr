using Whispr.Client.Models;
using Xunit;

namespace Whispr.Client.Tests.Models;

public sealed class ServerTreeNodeTests
{
    [Fact]
    public void IsAdmin_Setter_RaisesPropertyChanged()
    {
        var node = new ServerTreeNode { DisplayName = "Test", Kind = NodeKind.User };
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ServerTreeNode.IsAdmin))
                raised = true;
        };

        node.IsAdmin = true;
        Assert.True(raised);
        Assert.True(node.IsAdmin);
    }

    [Fact]
    public void IsAdmin_SetterSameValue_DoesNotRaisePropertyChanged()
    {
        var node = new ServerTreeNode { DisplayName = "Test", Kind = NodeKind.User };
        node.IsAdmin = true;
        var raised = false;
        node.PropertyChanged += (_, _) => raised = true;

        node.IsAdmin = true;
        Assert.False(raised);
    }

    [Fact]
    public void IsSpeaking_Setter_RaisesPropertyChanged()
    {
        var node = new ServerTreeNode { DisplayName = "Test", Kind = NodeKind.User };
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ServerTreeNode.IsSpeaking))
                raised = true;
        };

        node.IsSpeaking = true;
        Assert.True(raised);
        Assert.True(node.IsSpeaking);
    }

    [Fact]
    public void Children_IsEmptyByDefault()
    {
        var node = new ServerTreeNode { DisplayName = "Server", Kind = NodeKind.Server };
        Assert.Empty(node.Children);
    }

    [Fact]
    public void Children_CanAddNodes()
    {
        var root = new ServerTreeNode { DisplayName = "Server", Kind = NodeKind.Server };
        var channel = new ServerTreeNode { DisplayName = "General", Kind = NodeKind.Channel, ChannelId = Guid.NewGuid() };

        root.Children.Add(channel);
        Assert.Single(root.Children);
        Assert.Equal("General", root.Children[0].DisplayName);
        Assert.Equal(NodeKind.Channel, root.Children[0].Kind);
    }

    [Fact]
    public void NodeKind_ValuesAreCorrect()
    {
        Assert.Equal(0, (int)NodeKind.Server);
        Assert.Equal(1, (int)NodeKind.Channel);
        Assert.Equal(2, (int)NodeKind.User);
    }
}
