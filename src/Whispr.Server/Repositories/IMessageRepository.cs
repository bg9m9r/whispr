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
    /// Gets messages for a channel, optionally since a given timestamp. Ordered by CreatedAt ascending.
    /// </summary>
    IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, int limit = 100);
}

/// <summary>
/// A persisted message record.
/// </summary>
public sealed record MessageRecord(Guid Id, Guid ChannelId, Guid SenderId, string Content, DateTimeOffset CreatedAt);
