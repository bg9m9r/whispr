using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

internal sealed class MessageHandler(IAuthService auth, IMessageService messages, IChannelService channels) : IControlMessageHandler
{
    private static readonly string[] Types = [MessageTypes.SendMessage, MessageTypes.GetMessageHistory, MessageTypes.EditMessage, MessageTypes.DeleteMessage];

    public IReadOnlyList<string> HandledMessageTypes { get; } = Types;

    public Task HandleAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        return message.Type switch
        {
            MessageTypes.SendMessage => HandleSendMessageAsync(message, ctx),
            MessageTypes.GetMessageHistory => HandleGetMessageHistoryAsync(message, ctx),
            MessageTypes.EditMessage => HandleEditMessageAsync(message, ctx),
            MessageTypes.DeleteMessage => HandleDeleteMessageAsync(message, ctx),
            _ => Task.CompletedTask
        };
    }

    private async Task HandleSendMessageAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAuthAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<SendMessagePayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "SendMessage payload required");
            return;
        }

        var user = ctx.State.User!;
        if (!PayloadValidation.IsValidChannelId(payload.ChannelId, out var channelError))
        {
            await ctx.SendErrorAsync("invalid_payload", channelError!);
            return;
        }
        var channel = channels.GetChannel(payload.ChannelId);
        if (channel is null || channel.Type != ChannelType.Text)
        {
            await ctx.SendErrorAsync("access_denied", "Messages are only allowed in text channels");
            return;
        }
        var content = PayloadValidation.SanitizeMessageContent((payload.Content ?? "").Trim());
        if (!PayloadValidation.IsValidMessageContent(content, out var contentError))
        {
            await ctx.SendErrorAsync("invalid_payload", contentError!);
            return;
        }

        var record = messages.SendMessage(payload.ChannelId, user.Id, content);
        if (record is null)
        {
            await ctx.SendErrorAsync("access_denied", "You do not have permission to send messages in this channel");
            return;
        }

        var chatPayload = new ChatMessagePayload
        {
            Id = record.Id,
            ChannelId = record.ChannelId,
            SenderId = record.SenderId,
            SenderUsername = auth.GetUsername(record.SenderId) ?? record.SenderId.ToString(),
            Content = record.Content,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.MessageReceived, chatPayload);
        await ctx.SendToUsersWithChannelAccessAsync(payload.ChannelId, bytes, null, ctx.CancellationToken);
    }

    private async Task HandleGetMessageHistoryAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAuthAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<GetMessageHistoryPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "GetMessageHistory payload required");
            return;
        }
        if (!PayloadValidation.IsValidChannelId(payload.ChannelId, out var channelError))
        {
            await ctx.SendErrorAsync("invalid_payload", channelError!);
            return;
        }

        var user = ctx.State.User!;
        var limit = Math.Clamp(payload.Limit, 1, 500);
        var history = messages.GetHistory(payload.ChannelId, user.Id, payload.Since, payload.Before, limit);

        var chatMessages = history.Select(m => new ChatMessagePayload
        {
            Id = m.Id,
            ChannelId = m.ChannelId,
            SenderId = m.SenderId,
            SenderUsername = auth.GetUsername(m.SenderId) ?? m.SenderId.ToString(),
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        }).ToList();

        var response = ControlProtocol.Serialize(MessageTypes.MessageHistory, new MessageHistoryPayload
        {
            ChannelId = payload.ChannelId,
            Messages = chatMessages
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    private async Task HandleEditMessageAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAuthAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<EditMessagePayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "EditMessage payload required");
            return;
        }

        var user = ctx.State.User!;
        if (!PayloadValidation.IsValidChannelId(payload.ChannelId, out var channelError))
        {
            await ctx.SendErrorAsync("invalid_payload", channelError!);
            return;
        }
        var channel = channels.GetChannel(payload.ChannelId);
        if (channel is null || channel.Type != ChannelType.Text)
        {
            await ctx.SendErrorAsync("access_denied", "Only text channel messages can be edited");
            return;
        }
        var content = PayloadValidation.SanitizeMessageContent((payload.Content ?? "").Trim());
        if (!PayloadValidation.IsValidMessageContent(content, out var contentError))
        {
            await ctx.SendErrorAsync("invalid_payload", contentError!);
            return;
        }

        var record = messages.Update(payload.MessageId, user.Id, content);
        if (record is null)
        {
            await ctx.SendErrorAsync("access_denied", "Message not found or you can only edit your own messages");
            return;
        }

        var chatPayload = new ChatMessagePayload
        {
            Id = record.Id,
            ChannelId = record.ChannelId,
            SenderId = record.SenderId,
            SenderUsername = auth.GetUsername(record.SenderId) ?? record.SenderId.ToString(),
            Content = record.Content,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.MessageUpdated, chatPayload);
        await ctx.SendToUsersWithChannelAccessAsync(payload.ChannelId, bytes, null, ctx.CancellationToken);
    }

    private async Task HandleDeleteMessageAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAuthAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<DeleteMessagePayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "DeleteMessage payload required");
            return;
        }

        if (!PayloadValidation.IsValidChannelId(payload.ChannelId, out var channelError))
        {
            await ctx.SendErrorAsync("invalid_payload", channelError!);
            return;
        }

        var user = ctx.State.User!;
        var deleted = messages.Delete(payload.MessageId, user.Id);
        if (!deleted)
        {
            await ctx.SendErrorAsync("access_denied", "Message not found or only the sender or an admin can delete it");
            return;
        }

        var deletePayload = new MessageDeletedPayload
        {
            MessageId = payload.MessageId,
            ChannelId = payload.ChannelId
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.MessageDeleted, deletePayload);
        await ctx.SendToUsersWithChannelAccessAsync(payload.ChannelId, bytes, null, ctx.CancellationToken);
    }
}
