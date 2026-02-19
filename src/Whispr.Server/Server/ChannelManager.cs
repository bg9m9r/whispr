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
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, ChannelState> _channels = new();
    private readonly Dictionary<Guid, Guid> _userToChannel = new();
    private readonly IChannelRepository _store;

    private sealed class ChannelState
    {
        public required Channel Channel { get; set; }
        public byte[] KeyMaterial { get; init; } = [];
    }

    public ChannelManager(IChannelRepository store)
    {
        _store = store;
        var loaded = _store.LoadAll();
        var defaultCh = loaded.FirstOrDefault(c => c.IsDefault);
        if (defaultCh.Id == Guid.Empty && loaded.Count > 0)
            defaultCh = loaded[0];
        DefaultChannelId = defaultCh.Id != Guid.Empty ? defaultCh.Id : Guid.NewGuid();
        foreach (var (id, name, keyMaterial, _, type) in loaded)
        {
            _channels[id] = new ChannelState
            {
                Channel = new Channel { Id = id, Name = name, Type = type, MemberIds = [] },
                KeyMaterial = keyMaterial
            };
        }
        if (_channels.Count == 0)
        {
            var keyMaterial = RandomNumberGenerator.GetBytes(32);
            _channels[DefaultChannelId] = new ChannelState
            {
                Channel = new Channel { Id = DefaultChannelId, Name = "General", Type = ChannelType.Voice, MemberIds = [] },
                KeyMaterial = keyMaterial
            };
        }
    }

    /// <summary>
    /// Gets the default channel ID (user lands here on connect).
    /// </summary>
    public Guid DefaultChannelId { get; }

    /// <summary>
    /// Joins the default channel. Called when user connects.
    /// </summary>
    public (Channel Channel, byte[]? KeyMaterial)? JoinDefaultChannel(Guid userId)
    {
        return JoinChannel(DefaultChannelId, userId);
    }

    /// <summary>
    /// Creates a new channel. Returns null if at max (10) or name invalid.
    /// Text channels have no key material (no audio).
    /// </summary>
    public Channel? CreateChannel(string name, ChannelType type, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            if (_channels.Count >= MaxChannels)
                return null;

            var channelId = Guid.NewGuid();
            var keyMaterial = type == ChannelType.Voice ? RandomNumberGenerator.GetBytes(32) : Array.Empty<byte>();
            var channel = new Channel { Id = channelId, Name = name.Trim(), Type = type, MemberIds = [] };
            if (!_store.Insert(channelId, channel.Name, keyMaterial, type))
                return null;
            _channels[channelId] = new ChannelState { Channel = channel, KeyMaterial = keyMaterial };
            return channel;
        }
    }

    /// <summary>
    /// Joins a channel. Leaves current channel first. Returns null if channel not found.
    /// KeyMaterial is empty for text channels (no audio).
    /// </summary>
    public (Channel Channel, byte[]? KeyMaterial)? JoinChannel(Guid channelId, Guid userId)
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
            state.Channel = new Channel { Id = state.Channel.Id, Name = state.Channel.Name, Type = state.Channel.Type, MemberIds = members };
            _userToChannel[userId] = channelId;
            var key = state.KeyMaterial.Length > 0 ? state.KeyMaterial : null;
            return (state.Channel, key);
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
        if (!_userToChannel.Remove(userId, out var channelId))
            return null;

        if (!_channels.TryGetValue(channelId, out var state))
            return null;

        var members = state.Channel.MemberIds.Where(m => m != userId).ToList();
        state.Channel = new Channel { Id = state.Channel.Id, Name = state.Channel.Name, Type = state.Channel.Type, MemberIds = members };
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
            return !_channels.TryGetValue(channelId, out var state) 
                ? null 
                : state.Channel.MemberIds.Where(m => m != excludeUserId).ToList();
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
                    Type = s.Channel.Type == ChannelType.Text ? "text" : "voice",
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
