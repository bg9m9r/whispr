namespace Whispr.Core.Models;

/// <summary>
/// Control message type identifiers. "Room" is legacy terminology for channel.
/// </summary>
public static class MessageTypes
{
    // Client → Server
    public const string LoginRequest = "login";
    public const string CreateRoom = "create_room";
    public const string JoinRoom = "join_room";
    public const string LeaveRoom = "leave_room";
    public const string RegisterUdp = "register_udp";
    public const string RegisterUdpResponse = "register_udp_response";
    public const string RequestRoomList = "request_room_list";
    public const string RequestServerState = "request_server_state";
    public const string CreateChannel = "create_channel";
    public const string JoinChannel = "join_channel";
    public const string Ping = "ping";
    public const string ListPermissions = "list_permissions";
    public const string ListRoles = "list_roles";
    public const string GetUserPermissions = "get_user_permissions";
    public const string SetUserPermission = "set_user_permission";
    public const string SetUserRole = "set_user_role";
    public const string GetChannelPermissions = "get_channel_permissions";
    public const string SetChannelRolePermission = "set_channel_role_permission";
    public const string SetChannelUserPermission = "set_channel_user_permission";
    public const string SendMessage = "send_message";
    public const string GetMessageHistory = "get_message_history";
    public const string EditMessage = "edit_message";
    public const string DeleteMessage = "delete_message";

    // Server → Client
    public const string LoginResponse = "login_response";
    public const string RoomList = "room_list";
    public const string ServerState = "server_state";
    public const string RoomJoined = "room_joined";
    public const string RoomLeft = "room_left";
    public const string MemberJoined = "member_joined";
    public const string MemberLeft = "member_left";
    public const string MemberUdpRegistered = "member_udp_registered";
    public const string Pong = "pong";
    public const string Error = "error";
    public const string PermissionsList = "permissions_list";
    public const string RolesList = "roles_list";
    public const string UserPermissions = "user_permissions";
    public const string ChannelPermissions = "channel_permissions";
    public const string MessageReceived = "message_received";
    public const string MessageHistory = "message_history";
    public const string MessageUpdated = "message_updated";
    public const string MessageDeleted = "message_deleted";
}
