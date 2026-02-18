using System.Collections.Concurrent;
using System.Net;

namespace Whispr.Server.Server;

/// <summary>
/// Maps client IDs to users and UDP endpoints for audio relay.
/// </summary>
public sealed class UdpEndpointRegistry
{
    private readonly ConcurrentDictionary<uint, Guid> _clientIdToUserId = new();
    private readonly ConcurrentDictionary<Guid, uint> _userIdToClientId = new();
    private readonly ConcurrentDictionary<Guid, (uint ClientId, IPEndPoint Endpoint)> _userIdToEndpoint = new();

    /// <summary>
    /// Registers a client ID for a user (from RegisterUdp). Endpoint is learned from first UDP packet.
    /// </summary>
    public void RegisterClientId(uint clientId, Guid userId)
    {
        _clientIdToUserId[clientId] = userId;
        _userIdToClientId[userId] = clientId;
    }

    /// <summary>
    /// Gets the client ID for a user (if registered).
    /// </summary>
    public uint? GetClientId(Guid userId)
    {
        return _userIdToClientId.TryGetValue(userId, out var clientId) ? clientId : null;
    }

    /// <summary>
    /// Records the UDP endpoint when first packet is received from a client.
    /// </summary>
    public void RegisterEndpoint(uint clientId, IPEndPoint endpoint)
    {
        if (!_clientIdToUserId.TryGetValue(clientId, out var userId))
            return;

        _userIdToEndpoint[userId] = (clientId, endpoint);
    }

    /// <summary>
    /// Gets the user ID for a client ID.
    /// </summary>
    public Guid? GetUserId(uint clientId)
    {
        return _clientIdToUserId.TryGetValue(clientId, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets the UDP endpoint for a user.
    /// </summary>
    public IPEndPoint? GetEndpoint(Guid userId)
    {
        return _userIdToEndpoint.TryGetValue(userId, out var entry) ? entry.Endpoint : null;
    }

    /// <summary>
    /// Removes a user's registration (e.g. on leave/disconnect).
    /// </summary>
    public void Unregister(Guid userId)
    {
        if (_userIdToEndpoint.TryRemove(userId, out var entry))
            _clientIdToUserId.TryRemove(entry.ClientId, out _);
        _userIdToClientId.TryRemove(userId, out _);
    }

    /// <summary>
    /// Removes by client ID.
    /// </summary>
    public void UnregisterByClientId(uint clientId)
    {
        if (_clientIdToUserId.TryRemove(clientId, out var userId))
        {
            _userIdToEndpoint.TryRemove(userId, out _);
            _userIdToClientId.TryRemove(userId, out _);
        }
    }
}
