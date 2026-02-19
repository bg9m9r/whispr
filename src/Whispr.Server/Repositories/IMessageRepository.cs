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
    /// Gets messages for a channel. When since is set: messages after that time (ascending). When before is set: messages before that time (ascending). Otherwise: latest messages (ascending). Ordered by CreatedAt ascending.
    /// </summary>
    IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, DateTimeOffset? before = null, int limit = 100);
}

/// <summary>
/// A persisted message record.
/// </summary>
public sealed record MessageRecord(Guid Id, Guid ChannelId, Guid SenderId, string Content, DateTimeOffset CreatedAt);
