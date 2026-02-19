namespace Whispr.Server.Repositories;

/// <summary>
/// In-memory message store for when no database is configured.
/// </summary>
public sealed class InMemoryMessageRepository : IMessageRepository
{
    private readonly List<MessageRecord> _messages = [];
    private readonly object _lock = new();

    public MessageRecord Add(Guid channelId, Guid senderId, string content)
    {
        var id = Guid.NewGuid();
        var record = new MessageRecord(id, channelId, senderId, content, DateTimeOffset.UtcNow);
        lock (_lock)
        {
            _messages.Add(record);
        }
        return record;
    }

    public IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, int limit = 100)
    {
        lock (_lock)
        {
            return _messages
                .Where(m => m.ChannelId == channelId)
                .Where(m => since is null || m.CreatedAt > since)
                .OrderBy(m => m.CreatedAt)
                .Take(limit)
                .ToList();
        }
    }
}
