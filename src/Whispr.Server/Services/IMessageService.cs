using Whispr.Server.Repositories;

namespace Whispr.Server.Services;

/// <summary>
/// Domain service for chat messages.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a message to a channel. Returns the message id, or null if send failed (e.g. no channel access).
    /// </summary>
    MessageRecord? SendMessage(Guid channelId, Guid senderId, string content);

    /// <summary>
    /// Gets message history for a channel. Returns empty if no access.
    /// </summary>
    IReadOnlyList<MessageRecord> GetHistory(Guid channelId, Guid requesterId, DateTimeOffset? since = null, int limit = 100);
}
