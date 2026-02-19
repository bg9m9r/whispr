using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Server.Services;

/// <summary>
/// Channel domain service.
/// Manages voice channels: join, leave, create, member lists, per-channel keys.
/// </summary>
public interface IChannelService
{
    (Channel Channel, byte[]? KeyMaterial)? JoinDefaultChannel(Guid userId);
    (Channel Channel, byte[]? KeyMaterial)? JoinChannel(Guid channelId, Guid userId);
    (Guid ChannelId, IReadOnlyList<Guid> RemainingMembers)? LeaveChannel(Guid userId);
    Channel? CreateChannel(string name, ChannelType type, Guid userId);
    IReadOnlyList<ChannelInfo> ListChannels();
    Channel? GetChannel(Guid channelId);
    Guid? GetUserChannel(Guid userId);
    IReadOnlyList<Guid>? GetOtherMembers(Guid channelId, Guid excludeUserId);
    byte[]? GetChannelKeyMaterial(Guid channelId);
    bool CanCreateMoreChannels { get; }
}
