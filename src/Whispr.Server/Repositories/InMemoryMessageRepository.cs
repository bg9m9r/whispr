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

    public IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, DateTimeOffset? before = null, int limit = 100)
    {
        lock (_lock)
        {
            var filtered = _messages.Where(m => m.ChannelId == channelId);
            if (before is { } b)
                return filtered.Where(m => m.CreatedAt < b).OrderByDescending(m => m.CreatedAt).Take(limit).OrderBy(m => m.CreatedAt).ToList();
            if (since is { } s)
                return filtered.Where(m => m.CreatedAt > s).OrderBy(m => m.CreatedAt).Take(limit).ToList();
            return filtered.OrderByDescending(m => m.CreatedAt).Take(limit).OrderBy(m => m.CreatedAt).ToList();
        }
    }
}
