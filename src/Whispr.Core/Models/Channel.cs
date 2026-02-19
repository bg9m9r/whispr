namespace Whispr.Core.Models;

/// <summary>
/// Type of channel: voice (audio) or text (messages only).
/// </summary>
public enum ChannelType
{
    Voice = 0,
    Text = 1
}

/// <summary>
/// Represents a channel on the server (voice or text).
/// </summary>
public sealed class Channel
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ChannelType Type { get; init; }
    public required IReadOnlyList<Guid> MemberIds { get; init; }
}
