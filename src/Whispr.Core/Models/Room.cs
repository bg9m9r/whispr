namespace Whispr.Core.Models;

/// <summary>
/// Represents a voice chat room.
/// </summary>
public sealed class Room
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<Guid> MemberIds { get; init; }
}
