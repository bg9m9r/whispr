using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

internal sealed class MessageHandler(IAuthService auth, IMessageService messages) : IControlMessageHandler
{
    private static readonly string[] Types = [MessageTypes.SendMessage, MessageTypes.GetMessageHistory];

    public IReadOnlyList<string> HandledMessageTypes { get; } = Types;

    public Task HandleAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        return message.Type switch
        {
            MessageTypes.SendMessage => HandleSendMessageAsync(message, ctx),
            MessageTypes.GetMessageHistory => HandleGetMessageHistoryAsync(message, ctx),
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
            CreatedAt = record.CreatedAt
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.MessageReceived, chatPayload);
        await ctx.SendToChannelAsync(payload.ChannelId, bytes, null, ctx.CancellationToken);
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
        var history = messages.GetHistory(payload.ChannelId, user.Id, payload.Since, limit);

        var chatMessages = history.Select(m => new ChatMessagePayload
        {
            Id = m.Id,
            ChannelId = m.ChannelId,
            SenderId = m.SenderId,
            SenderUsername = auth.GetUsername(m.SenderId) ?? m.SenderId.ToString(),
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();

        var response = ControlProtocol.Serialize(MessageTypes.MessageHistory, new MessageHistoryPayload
        {
            ChannelId = payload.ChannelId,
            Messages = chatMessages
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }
}
