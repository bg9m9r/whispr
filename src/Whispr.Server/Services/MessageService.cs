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

    public IReadOnlyList<MessageRecord> GetHistory(Guid channelId, Guid requesterId, DateTimeOffset? since = null, int limit = 100)
    {
        if (!auth.CanAccessChannel(requesterId, channelId))
            return [];

        return messages.GetByChannel(channelId, since, Math.Clamp(limit, 1, 500));
    }
}
