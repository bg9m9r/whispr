using Whispr.Core.Protocol;

namespace Whispr.Client.Models;

/// <summary>
/// Wraps a chat message for display. When ShowSenderHeader is true, the sender name and timestamp are shown (e.g. first message or when sender changes).
/// </summary>
public sealed record MessageDisplayItem(ChatMessagePayload Message, bool ShowSenderHeader);
