using System.Net;
using Whispr.Server.Server;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class UdpEndpointRegistryTests
{
    [Fact]
    public void AssignClientId_AndGetClientId_ReturnsId()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        var clientId = registry.AssignClientId(userId);
        Assert.Equal(clientId, registry.GetClientId(userId));
    }

    [Fact]
    public void AssignClientId_AssignsUniqueIds()
    {
        var registry = new UdpEndpointRegistry();
        var id1 = registry.AssignClientId(Guid.NewGuid());
        var id2 = registry.AssignClientId(Guid.NewGuid());
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GetClientId_UnregisteredUser_ReturnsNull()
    {
        var registry = new UdpEndpointRegistry();
        Assert.Null(registry.GetClientId(Guid.NewGuid()));
    }

    [Fact]
    public void GetUserId_ReturnsUserId()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        var clientId = registry.AssignClientId(userId);
        Assert.Equal(userId, registry.GetUserId(clientId));
    }

    [Fact]
    public void RegisterEndpoint_AndGetEndpoint_ReturnsEndpoint()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        var clientId = registry.AssignClientId(userId);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
        registry.RegisterEndpoint(clientId, endpoint);
        Assert.Equal(endpoint, registry.GetEndpoint(userId));
    }

    [Fact]
    public void UnregisterByClientId_RemovesRegistration()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        var clientId = registry.AssignClientId(userId);
        registry.UnregisterByClientId(clientId);
        Assert.Null(registry.GetClientId(userId));
        Assert.Null(registry.GetUserId(clientId));
    }

    [Fact]
    public void Unregister_RemovesRegistration()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        var clientId = registry.AssignClientId(userId);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
        registry.RegisterEndpoint(clientId, endpoint);
        registry.Unregister(userId);
        Assert.Null(registry.GetClientId(userId));
        Assert.Null(registry.GetUserId(clientId));
        Assert.Null(registry.GetEndpoint(userId));
    }
}
