namespace Whispr.Client.Models;

/// <summary>
/// A segment of message content: either plain text or a clickable URL.
/// </summary>
public sealed record MessageContentSegment(string Content, bool IsLink);
