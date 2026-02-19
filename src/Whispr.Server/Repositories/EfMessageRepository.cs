using Microsoft.EntityFrameworkCore;
using Whispr.Server.Data;
using Whispr.Server.Services;

namespace Whispr.Server.Repositories;

/// <summary>
/// EF Core implementation of message persistence. Encrypts content at rest when IMessageEncryption is provided.
/// </summary>
public sealed class EfMessageRepository(IDbContextFactory<WhisprDbContext> factory, IMessageEncryption encryption) : IMessageRepository
{
    private const string EncryptedPrefix = "enc:";

    public MessageRecord Add(Guid channelId, Guid senderId, string content)
    {
        var encrypted = encryption.Encrypt(content);
        var stored = EncryptedPrefix + Convert.ToBase64String(encrypted);

        using var ctx = factory.CreateDbContext();
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var entity = new MessageEntity
        {
            Id = id.ToString(),
            ChannelId = channelId.ToString(),
            SenderId = senderId.ToString(),
            Content = stored,
            CreatedAt = createdAt,
            CreatedAtTicks = createdAt.UtcTicks
        };
        ctx.Messages.Add(entity);
        ctx.SaveChanges();
        return new MessageRecord(id, channelId, senderId, content, createdAt);
    }

    public IReadOnlyList<MessageRecord> GetByChannel(Guid channelId, DateTimeOffset? since = null, DateTimeOffset? before = null, int limit = 100)
    {
        using var ctx = factory.CreateDbContext();
        var channelIdStr = channelId.ToString();
        var query = ctx.Messages.Where(m => m.ChannelId == channelIdStr);

        List<MessageEntity> list;
        if (before is { } b)
        {
            var beforeTicks = b.UtcTicks;
            list = query
                .Where(m => m.CreatedAtTicks < beforeTicks)
                .OrderByDescending(m => m.CreatedAtTicks)
                .Take(limit)
                .ToList();
            list.Reverse(); // return ascending (oldest first in page)
        }
        else if (since is { } s)
        {
            var sinceTicks = s.UtcTicks;
            list = query
                .Where(m => m.CreatedAtTicks > sinceTicks)
                .OrderBy(m => m.CreatedAtTicks)
                .Take(limit)
                .ToList();
        }
        else
        {
            list = query
                .OrderByDescending(m => m.CreatedAtTicks)
                .Take(limit)
                .ToList();
            list.Reverse(); // oldest first
        }

        return list.Select(m =>
        {
            var content = m.Content.StartsWith(EncryptedPrefix, StringComparison.Ordinal)
                ? encryption.Decrypt(Convert.FromBase64String(m.Content.Substring(EncryptedPrefix.Length)))
                : m.Content;
            return new MessageRecord(
                Guid.Parse(m.Id),
                Guid.Parse(m.ChannelId),
                Guid.Parse(m.SenderId),
                content,
                m.CreatedAt);
        }).ToList();
    }
}
