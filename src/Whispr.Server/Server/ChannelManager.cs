using System.Security.Cryptography;
using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Repositories;
using Whispr.Server.Services;

namespace Whispr.Server.Server;

/// <summary>
/// Manages voice channels: default channel, create (up to 10), join, leave, per-channel keys.
/// Channels are persisted via IChannelRepository.
/// </summary>
public sealed class ChannelManager : IChannelService
{
    private const int MaxChannels = 10;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, ChannelState> _channels = new();
    private readonly Dictionary<Guid, Guid> _userToChannel = new();
    private readonly Guid _defaultChannelId;
    private readonly IChannelRepository _store;

    private sealed class ChannelState
    {
        public required Channel Channel { get; set; }
        public required byte[] KeyMaterial { get; init; }
    }

    public ChannelManager(IChannelRepository store)
    {
        _store = store;
        var loaded = _store.LoadAll();
        var defaultCh = loaded.FirstOrDefault(c => c.IsDefault);
        if (defaultCh.Id == default && loaded.Count > 0)
            defaultCh = loaded[0];
        _defaultChannelId = defaultCh.Id != default ? defaultCh.Id : Guid.NewGuid();
        foreach (var (id, name, keyMaterial, _) in loaded)
        {
            _channels[id] = new ChannelState
            {
                Channel = new Channel { Id = id, Name = name, MemberIds = [] },
                KeyMaterial = keyMaterial
            };
        }
        if (_channels.Count == 0)
        {
            var keyMaterial = RandomNumberGenerator.GetBytes(32);
            _channels[_defaultChannelId] = new ChannelState
            {
                Channel = new Channel { Id = _defaultChannelId, Name = "General", MemberIds = [] },
                KeyMaterial = keyMaterial
            };
        }
    }

    /// <summary>
    /// Gets the default channel ID (user lands here on connect).
    /// </summary>
    public Guid DefaultChannelId => _defaultChannelId;

    /// <summary>
    /// Joins the default channel. Called when user connects.
    /// </summary>
    public (Channel Channel, byte[] KeyMaterial)? JoinDefaultChannel(Guid userId)
    {
        return JoinChannel(_defaultChannelId, userId);
    }

    /// <summary>
    /// Creates a new channel. Returns null if at max (10) or name invalid.
    /// </summary>
    public Channel? CreateChannel(string name, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            if (_channels.Count >= MaxChannels)
                return null;

            var channelId = Guid.NewGuid();
            var keyMaterial = RandomNumberGenerator.GetBytes(32);
            var channel = new Channel { Id = channelId, Name = name.Trim(), MemberIds = [] };
            if (!_store.Insert(channelId, channel.Name, keyMaterial))
                return null;
            _channels[channelId] = new ChannelState { Channel = channel, KeyMaterial = keyMaterial };
            return channel;
        }
    }

    /// <summary>
    /// Joins a channel. Leaves current channel first. Returns null if channel not found.
    /// </summary>
    public (Channel Channel, byte[] KeyMaterial)? JoinChannel(Guid channelId, Guid userId)
    {
        lock (_lock)
        {
            if (_userToChannel.TryGetValue(userId, out var currentChannelId))
            {
                if (currentChannelId == channelId)
                    return null;
                LeaveChannelInternal(userId);
            }

            if (!_channels.TryGetValue(channelId, out var state))
                return null;

            var members = state.Channel.MemberIds.ToList();
            if (members.Contains(userId))
                return null;

            members.Add(userId);
            state.Channel = new Channel { Id = state.Channel.Id, Name = state.Channel.Name, MemberIds = members };
            _userToChannel[userId] = channelId;
            return (state.Channel, state.KeyMaterial);
        }
    }

    /// <summary>
    /// Leaves the current channel. Returns (channelId, remaining members) for notification.
    /// </summary>
    public (Guid ChannelId, IReadOnlyList<Guid> RemainingMembers)? LeaveChannel(Guid userId)
    {
        lock (_lock)
        {
            return LeaveChannelInternal(userId);
        }
    }

    private (Guid ChannelId, IReadOnlyList<Guid> RemainingMembers)? LeaveChannelInternal(Guid userId)
    {
        if (!_userToChannel.TryGetValue(userId, out var channelId))
            return null;

        _userToChannel.Remove(userId);

        if (!_channels.TryGetValue(channelId, out var state))
            return null;

        var members = state.Channel.MemberIds.Where(m => m != userId).ToList();
        state.Channel = new Channel { Id = state.Channel.Id, Name = state.Channel.Name, MemberIds = members };
        return (channelId, members);
    }

    public Guid? GetUserChannel(Guid userId)
    {
        lock (_lock)
            return _userToChannel.TryGetValue(userId, out var c) ? c : null;
    }

    public IReadOnlyList<Guid>? GetOtherMembers(Guid channelId, Guid excludeUserId)
    {
        lock (_lock)
        {
            if (!_channels.TryGetValue(channelId, out var state))
                return null;
            return state.Channel.MemberIds.Where(m => m != excludeUserId).ToList();
        }
    }

    public Channel? GetChannel(Guid channelId)
    {
        lock (_lock)
            return _channels.TryGetValue(channelId, out var s) ? s.Channel : null;
    }

    public byte[]? GetChannelKeyMaterial(Guid channelId)
    {
        lock (_lock)
            return _channels.TryGetValue(channelId, out var s) ? s.KeyMaterial : null;
    }

    /// <summary>
    /// Lists all channels with id, name, and members.
    /// </summary>
    public IReadOnlyList<ChannelInfo> ListChannels()
    {
        lock (_lock)
        {
            return _channels.Values
                .Select(s => new ChannelInfo
                {
                    Id = s.Channel.Id,
                    Name = s.Channel.Name,
                    MemberIds = s.Channel.MemberIds
                })
                .ToList();
        }
    }

    public bool CanCreateMoreChannels
    {
        get { lock (_lock) return _channels.Count < MaxChannels; }
    }
}
