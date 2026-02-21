using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Control channel reader, server state, room operations, and permissions protocol.
/// </summary>
public interface IChannelService : IDisposable
{
    ServerStatePayload ServerState { get; }
    ChannelJoinedResult ChannelResult { get; }
    IReadOnlyDictionary<Guid, uint> UserIdToClientId { get; }
    IReadOnlyDictionary<Guid, string> Members { get; }

    /// <summary>Last measured round-trip latency in ms. Null = unknown, -1 = timeout.</summary>
    int? PingLatencyMs { get; }

    event Action<ServerStatePayload>? ServerStateReceived;
    event Action<ChannelJoinedResult>? RoomJoinedReceived;
    event Action? RoomLeftReceived;
    event Action<int?>? PingLatencyUpdated;
    event Action<ChatMessagePayload>? MessageReceived;
    event Action<MessageHistoryPayload>? MessageHistoryReceived;
    event Action<ChatMessagePayload>? MessageUpdated;
    event Action<MessageDeletedPayload>? MessageDeleted;

    void Start(ChannelJoinedResult roomResult, ServerStatePayload serverState);
    void Stop();

    Task<uint> RegisterUdpAsync(CancellationToken ct = default);
    Task RequestServerStateAsync();
    Task<ChannelJoinedResult?> SwitchToChannelAsync(Guid channelId);
    Task<ChannelJoinedResult?> CreateChannelAsync(string name, string type = "voice");
    Task LeaveRoomAsync();

    Task<PermissionsListPayload?> RequestPermissionsListAsync();
    Task<RolesListPayload?> RequestRolesListAsync();
    Task<UserPermissionsPayload?> RequestUserPermissionsAsync(Guid userId);
    Task<UserPermissionsPayload?> SetUserPermissionAsync(Guid userId, string permissionId, string? state);
    Task<UserPermissionsPayload?> SetUserRoleAsync(Guid userId, string roleId, bool assign);

    Task<ChannelPermissionsPayload?> RequestChannelPermissionsAsync(Guid channelId);
    Task<ChannelPermissionsPayload?> SetChannelRolePermissionAsync(Guid channelId, string roleId, string? state);
    Task<ChannelPermissionsPayload?> SetChannelUserPermissionAsync(Guid channelId, Guid userId, string? state);

    string? GetUsernameForUserId(Guid userId);
    IReadOnlyList<MemberInfo> GetChannelMembers(Guid channelId);

    Task SendMessageAsync(Guid channelId, string content, CancellationToken ct = default);
    Task RequestMessageHistoryAsync(Guid channelId, DateTimeOffset? since = null, DateTimeOffset? before = null, int limit = 100, CancellationToken ct = default);
    Task EditMessageAsync(Guid channelId, Guid messageId, string content, CancellationToken ct = default);
    Task DeleteMessageAsync(Guid channelId, Guid messageId, CancellationToken ct = default);
}
