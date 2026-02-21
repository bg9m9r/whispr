using Whispr.Server.Repositories;

namespace Whispr.Server.Services;

/// <summary>
/// Domain service for chat messages. Persists and retrieves via IMessageRepository.
/// </summary>
public sealed class MessageService(IMessageRepository messages, IAuthService auth) : IMessageService
{
    public MessageRecord? SendMessage(Guid channelId, Guid senderId, string content)
    {
        if (!auth.CanAccessChannel(senderId, channelId))
            return null;

        return messages.Add(channelId, senderId, content);
    }

    public IReadOnlyList<MessageRecord> GetHistory(Guid channelId, Guid requesterId, DateTimeOffset? since = null, DateTimeOffset? before = null, int limit = 100)
    {
        if (!auth.CanAccessChannel(requesterId, channelId))
            return [];

        return messages.GetByChannel(channelId, since, before, Math.Clamp(limit, 1, 500));
    }

    public MessageRecord? Update(Guid messageId, Guid requesterId, string content)
    {
        var existing = messages.GetById(messageId);
        if (existing is null || existing.SenderId != requesterId)
            return null;
        return messages.Update(messageId, content);
    }

    public bool Delete(Guid messageId, Guid requesterId)
    {
        var existing = messages.GetById(messageId);
        if (existing is null)
            return false;
        if (existing.SenderId != requesterId && !auth.IsAdmin(requesterId))
            return false;
        return messages.Delete(messageId);
    }
}
