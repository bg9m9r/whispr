namespace Whispr.Server.Repositories;

/// <summary>
/// Repository for chat message persistence.
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Adds a message. Returns the created message record.
    /// </summary>
    MessageRecord Add(Guid channelId, Guid senderId, string content);

    /// <summary>
    /// Gets a message by id, or null if not found.
    /// </summary>
    MessageRecord? GetById(Guid messageId);

    /// <summary>
    /// Gets messages for a channel. When since is set: messages after that time (ascending). When before is set: messages before that time (ascending). Otherwise: latest messages (ascending). Ordered by CreatedAt ascending.
    /// </summary>
    IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, DateTimeOffset? before = null, int limit = 100);

    /// <summary>
    /// Updates message content. Caller must verify ownership. Returns updated record or null if not found.
    /// </summary>
    MessageRecord? Update(Guid messageId, string content);

    /// <summary>
    /// Deletes a message. Returns true if deleted.
    /// </summary>
    bool Delete(Guid messageId);
}

/// <summary>
/// A persisted message record.
/// </summary>
public sealed record MessageRecord(Guid Id, Guid ChannelId, Guid SenderId, string Content, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt = null);
