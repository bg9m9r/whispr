namespace Whispr.Client.Models;

/// <summary>
/// Display model for a room participant.
/// </summary>
public sealed class ParticipantItem
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsMe { get; init; }
    public bool IsSpeaking { get; set; }
}
