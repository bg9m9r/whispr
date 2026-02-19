using System.Collections.Concurrent;
using System.Net;
using Whispr.Core.Models;

namespace Whispr.Server.Server;

/// <summary>
/// Per-connection session state.
/// </summary>
public sealed class SessionState
{
    public User? User { get; set; }
    public string? Token { get; set; }
    public Guid? RoomId { get; set; }
    public uint? ClientId { get; set; }
    public IPEndPoint? UdpEndpoint { get; set; }
    public Stream? ControlStream { get; set; }

    private readonly ConcurrentQueue<DateTime> _messageTimestamps = new();
    private const int MaxMessagesPerMinute = 120;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Returns true if the connection is within rate limits. Call before processing each message.
    /// </summary>
    public bool TryConsumeRateLimit()
    {
        var now = DateTime.UtcNow;
        _messageTimestamps.Enqueue(now);
        while (_messageTimestamps.TryPeek(out var oldest) && now - oldest > RateLimitWindow)
            _messageTimestamps.TryDequeue(out _);
        return _messageTimestamps.Count <= MaxMessagesPerMinute;
    }
}
