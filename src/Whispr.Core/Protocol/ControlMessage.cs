using System.Text.Json;
using System.Text.Json.Serialization;

namespace Whispr.Core.Protocol;

/// <summary>
/// Base control message with type and optional payload.
/// </summary>
public sealed class ControlMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }
}

/// <summary>
/// Strongly-typed payload for login request.
/// </summary>
public sealed class LoginRequestPayload
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }
}

/// <summary>
/// Strongly-typed payload for login response.
/// </summary>
public sealed class LoginResponsePayload
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("userId")]
    public Guid? UserId { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Payload for create channel (wire: create_room/create_channel).
/// </summary>
public sealed class CreateRoomPayload
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

/// <summary>
/// Payload for join channel (wire: join_room/join_channel).
/// </summary>
public sealed class JoinRoomPayload
{
    [JsonPropertyName("roomId")]
    public required Guid RoomId { get; init; }
}

/// <summary>
/// Channel summary in list response (wire: "rooms" array).
/// </summary>
public sealed class RoomInfo
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; init; }
}

/// <summary>
/// Channel info with members for tree display.
/// </summary>
public sealed class ChannelInfo
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("memberIds")]
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];

    [JsonPropertyName("members")]
    public IReadOnlyList<MemberInfo> Members { get; init; } = [];
}

/// <summary>
/// Server state: channels with members (for tree view).
/// </summary>
public sealed class ServerStatePayload
{
    [JsonPropertyName("channels")]
    public required IReadOnlyList<ChannelInfo> Channels { get; init; }

    [JsonPropertyName("canCreateChannel")]
    public bool CanCreateChannel { get; init; }
}

/// <summary>
/// Strongly-typed payload for room list response.
/// </summary>
public sealed class RoomListPayload
{
    [JsonPropertyName("rooms")]
    public required IReadOnlyList<RoomInfo> Rooms { get; init; }
}

/// <summary>
/// Member info for room display.
/// </summary>
public sealed class MemberInfo
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("clientId")]
    public uint ClientId { get; init; }

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; init; }
}

/// <summary>
/// Payload for channel joined (wire: room_joined).
/// </summary>
public sealed class RoomJoinedPayload
{
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; init; }

    [JsonPropertyName("roomName")]
    public string RoomName { get; init; } = string.Empty;

    [JsonPropertyName("memberIds")]
    public required IReadOnlyList<Guid> MemberIds { get; init; }

    [JsonPropertyName("members")]
    public IReadOnlyList<MemberInfo>? Members { get; init; }

    [JsonPropertyName("keyMaterial")]
    public required byte[] KeyMaterial { get; init; }
}

/// <summary>
/// Strongly-typed payload for register UDP.
/// </summary>
public sealed class RegisterUdpPayload
{
    [JsonPropertyName("clientId")]
    public required uint ClientId { get; init; }
}

/// <summary>
/// Payload for sending a chat message.
/// </summary>
public sealed class SendMessagePayload
{
    [JsonPropertyName("channelId")]
    public Guid ChannelId { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>
/// Payload for requesting message history.
/// </summary>
public sealed class GetMessageHistoryPayload
{
    [JsonPropertyName("channelId")]
    public Guid ChannelId { get; init; }

    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 100;
}

/// <summary>
/// A chat message (sent to client or in history).
/// </summary>
public sealed class ChatMessagePayload
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("channelId")]
    public Guid ChannelId { get; init; }

    [JsonPropertyName("senderId")]
    public Guid SenderId { get; init; }

    [JsonPropertyName("senderUsername")]
    public string SenderUsername { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Payload for message history response.
/// </summary>
public sealed class MessageHistoryPayload
{
    [JsonPropertyName("channelId")]
    public Guid ChannelId { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<ChatMessagePayload> Messages { get; init; }
}

/// <summary>
/// Strongly-typed payload for member joined/left.
/// </summary>
public sealed class MemberPayload
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("clientId")]
    public uint ClientId { get; init; }
}

/// <summary>
/// Strongly-typed payload for error messages.
/// </summary>
public sealed class ErrorPayload
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Permission state: Allow grants, Deny overrides, Neutral neither grants nor denies.
/// </summary>
public enum PermissionState
{
    Allow = 0,
    Deny = 1,
    Neutral = 2
}

/// <summary>
/// Permission definition.
/// </summary>
public sealed class PermissionInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Role with its permission assignments.
/// </summary>
public sealed class RoleInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("permissions")]
    public IReadOnlyList<RolePermissionAssignment> Permissions { get; init; } = [];
}

/// <summary>
/// Permission assignment within a role.
/// </summary>
public sealed class RolePermissionAssignment
{
    [JsonPropertyName("permissionId")]
    public required string PermissionId { get; init; }

    [JsonPropertyName("state")]
    public string State { get; init; } = "allow"; // allow, deny, neutral
}

/// <summary>
/// User's permission assignment (direct or from role).
/// </summary>
public sealed class UserPermissionAssignment
{
    [JsonPropertyName("permissionId")]
    public required string PermissionId { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; } // allow, deny, neutral
}

/// <summary>
/// Payload for permissions list response.
/// </summary>
public sealed class PermissionsListPayload
{
    [JsonPropertyName("permissions")]
    public required IReadOnlyList<PermissionInfo> Permissions { get; init; }
}

/// <summary>
/// Payload for roles list response.
/// </summary>
public sealed class RolesListPayload
{
    [JsonPropertyName("roles")]
    public required IReadOnlyList<RoleInfo> Roles { get; init; }
}

/// <summary>
/// Payload for get user permissions request.
/// </summary>
public sealed class GetUserPermissionsPayload
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }
}

/// <summary>
/// Payload for user permissions response.
/// </summary>
public sealed class UserPermissionsPayload
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("permissions")]
    public required IReadOnlyList<UserPermissionAssignment> Permissions { get; init; }

    [JsonPropertyName("roleIds")]
    public required IReadOnlyList<string> RoleIds { get; init; }
}

/// <summary>
/// Payload for set user permission.
/// </summary>
public sealed class SetUserPermissionPayload
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("permissionId")]
    public required string PermissionId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; } // allow, deny, neutral, or null to remove
}

/// <summary>
/// Payload for set user role.
/// </summary>
public sealed class SetUserRolePayload
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("roleId")]
    public required string RoleId { get; init; }

    [JsonPropertyName("assign")]
    public bool Assign { get; init; } // true = add role, false = remove role
}

/// <summary>
/// Payload for get channel permissions request.
/// </summary>
public sealed class GetChannelPermissionsPayload
{
    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; init; }
}

/// <summary>
/// Payload for channel permissions response (channel_access).
/// </summary>
public sealed class ChannelPermissionsPayload
{
    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; init; }

    [JsonPropertyName("roleStates")]
    public required IReadOnlyList<ChannelRoleState> RoleStates { get; init; }

    [JsonPropertyName("userStates")]
    public required IReadOnlyList<ChannelUserState> UserStates { get; init; }
}

public sealed class ChannelRoleState
{
    [JsonPropertyName("roleId")]
    public required string RoleId { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; } // allow, deny, neutral
}

public sealed class ChannelUserState
{
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; } // allow, deny, neutral
}

/// <summary>
/// Payload for set channel role permission.
/// </summary>
public sealed class SetChannelRolePermissionPayload
{
    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; init; }

    [JsonPropertyName("roleId")]
    public required string RoleId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; } // allow, deny, neutral, or null to remove
}

/// <summary>
/// Payload for set channel user permission.
/// </summary>
public sealed class SetChannelUserPermissionPayload
{
    [JsonPropertyName("channelId")]
    public required Guid ChannelId { get; init; }

    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; } // allow, deny, neutral, or null to remove
}
