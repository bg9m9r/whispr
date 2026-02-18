namespace Whispr.Core.Models;

/// <summary>
/// Represents a voice channel on the server.
/// </summary>
public sealed class Channel
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<Guid> MemberIds { get; init; }
}
