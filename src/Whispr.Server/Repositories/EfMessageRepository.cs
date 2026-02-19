using Microsoft.EntityFrameworkCore;
using Whispr.Server.Data;

namespace Whispr.Server.Repositories;

/// <summary>
/// EF Core implementation of message persistence.
/// </summary>
public sealed class EfMessageRepository(IDbContextFactory<WhisprDbContext> factory) : IMessageRepository
{
    public MessageRecord Add(Guid channelId, Guid senderId, string content)
    {
        using var ctx = factory.CreateDbContext();
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var entity = new MessageEntity
        {
            Id = id.ToString(),
            ChannelId = channelId.ToString(),
            SenderId = senderId.ToString(),
            Content = content,
            CreatedAt = createdAt
        };
        ctx.Messages.Add(entity);
        ctx.SaveChanges();
        return new MessageRecord(id, channelId, senderId, content, createdAt);
    }

    public IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, int limit = 100)
    {
        using var ctx = factory.CreateDbContext();
        var channelIdStr = channelId.ToString();
        var query = ctx.Messages.Where(m => m.ChannelId == channelIdStr);

        if (since is { } s)
            query = query.Where(m => m.CreatedAt > s);

        return query
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new MessageRecord(
                Guid.Parse(m.Id),
                Guid.Parse(m.ChannelId),
                Guid.Parse(m.SenderId),
                m.Content,
                m.CreatedAt))
            .ToList();
    }
}
