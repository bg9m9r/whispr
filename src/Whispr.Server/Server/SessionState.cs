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
}
