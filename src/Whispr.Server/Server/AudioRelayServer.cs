using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Whispr.Core.Protocol;
using Whispr.Server.Services;
using Whispr.Server.Handlers;

namespace Whispr.Server.Server;

/// <summary>
/// UDP audio relay server. Forwards encrypted packets between room members.
/// </summary>
public sealed class AudioRelayServer
{
    private const int MaxPacketsPerSecondPerClient = 100;

    private readonly int _port;
    private readonly IAuthService _auth;
    private readonly IChannelService _channels;
    private readonly UdpEndpointRegistry _udpRegistry;
    private readonly ConcurrentDictionary<uint, int> _packetCountByClient = new();
    private readonly object _rateLimitLock = new();
    private readonly Dictionary<uint, (int Count, long WindowStartMs)> _rateLimitByClient = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;

    public AudioRelayServer(ServerOptions options, IAuthService auth, IChannelService channels, UdpEndpointRegistry udpRegistry)
    {
        _port = options.AudioPort;
        _auth = auth;
        _channels = channels;
        _udpRegistry = udpRegistry;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _udpClient = new UdpClient(_port);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ServerLog.Info($"Audio relay listening on port {_port} (UDP)");

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(_cts.Token);
                ProcessPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ServerLog.Error($"UDP receive: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
    }

    private void ProcessPacket(byte[] packet, IPEndPoint remoteEndPoint)
    {
        if (!AudioProtocol.TryParsePacket(packet, out var clientId, out _, out var ciphertextWithTag))
            return;

        if (!TryConsumeRateLimit(clientId))
            return;

        var userId = _udpRegistry.GetUserId(clientId);
        if (userId is null)
            return;

        _udpRegistry.RegisterEndpoint(clientId, remoteEndPoint);

        var channelId = _channels.GetUserChannel(userId.Value);
        if (channelId is null)
            return;

        var otherMembers = _channels.GetOtherMembers(channelId.Value, userId.Value);
        if (otherMembers is null)
            return;

        var count = _packetCountByClient.AddOrUpdate(clientId, 1, (_, c) => c + 1);
        if (count == 1)
        {
            var username = _auth.GetUsername(userId.Value) ?? userId.Value.ToString();
            ServerLog.Info($"Audio started: {username} (clientId={clientId})");
        }
        else if (count % 50 == 0)
        {
            var username = _auth.GetUsername(userId.Value) ?? userId.Value.ToString();
            ServerLog.Info($"Audio: {username} → {otherMembers.Count} recipient(s) ({count} packets)");
        }

        foreach (var memberId in otherMembers)
        {
            var endpoint = _udpRegistry.GetEndpoint(memberId);
            if (endpoint is null)
                continue;

            try
            {
                _udpClient!.Send(packet, packet.Length, endpoint);
            }
            catch (Exception ex)
            {
                ServerLog.Error($"UDP send to {endpoint}: {ex.Message}");
            }
        }
    }

    private bool TryConsumeRateLimit(uint clientId)
    {
        var now = Environment.TickCount64;
        const int windowMs = 1000;

        lock (_rateLimitLock)
        {
            if (!_rateLimitByClient.TryGetValue(clientId, out var entry))
            {
                _rateLimitByClient[clientId] = (1, now);
                return true;
            }
            var elapsed = now - entry.WindowStartMs;
            if (elapsed >= windowMs)
            {
                _rateLimitByClient[clientId] = (1, now);
                return true;
            }
            if (entry.Count >= MaxPacketsPerSecondPerClient)
                return false;
            _rateLimitByClient[clientId] = (entry.Count + 1, entry.WindowStartMs);
            return true;
        }
    }
}
