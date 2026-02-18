using System.Net;
using Whispr.Server.Server;
using Xunit;

namespace Whispr.Server.Tests;

public sealed class UdpEndpointRegistryTests
{
    [Fact]
    public void RegisterClientId_AndGetClientId_ReturnsId()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        registry.RegisterClientId(42, userId);
        Assert.Equal(42u, registry.GetClientId(userId));
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
        registry.RegisterClientId(42, userId);
        Assert.Equal(userId, registry.GetUserId(42));
    }

    [Fact]
    public void RegisterEndpoint_AndGetEndpoint_ReturnsEndpoint()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        registry.RegisterClientId(42, userId);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
        registry.RegisterEndpoint(42, endpoint);
        Assert.Equal(endpoint, registry.GetEndpoint(userId));
    }

    [Fact]
    public void UnregisterByClientId_RemovesRegistration()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        registry.RegisterClientId(42, userId);
        registry.UnregisterByClientId(42);
        Assert.Null(registry.GetClientId(userId));
        Assert.Null(registry.GetUserId(42));
    }

    [Fact]
    public void Unregister_RemovesRegistration()
    {
        var registry = new UdpEndpointRegistry();
        var userId = Guid.NewGuid();
        registry.RegisterClientId(42, userId);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
        registry.RegisterEndpoint(42, endpoint);
        registry.Unregister(userId);
        Assert.Null(registry.GetClientId(userId));
        Assert.Null(registry.GetUserId(42));
        Assert.Null(registry.GetEndpoint(userId));
    }
}
