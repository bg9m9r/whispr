using Whispr.Core.Models;
using Whispr.Core.Protocol;
using Whispr.Server.Services;
using MessageTypes = Whispr.Core.Models.MessageTypes;

namespace Whispr.Server.Handlers;

internal sealed class PermissionHandler(IAuthService auth) : IControlMessageHandler
{
    private static readonly string[] Types =
    [
        MessageTypes.ListPermissions,
        MessageTypes.ListRoles,
        MessageTypes.GetUserPermissions,
        MessageTypes.SetUserPermission,
        MessageTypes.SetUserRole,
        MessageTypes.GetChannelPermissions,
        MessageTypes.SetChannelRolePermission,
        MessageTypes.SetChannelUserPermission
    ];

    public IReadOnlyList<string> HandledMessageTypes { get; } = Types;

    public Task HandleAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        return message.Type switch
        {
            MessageTypes.ListPermissions => HandleListPermissionsAsync(ctx),
            MessageTypes.ListRoles => HandleListRolesAsync(ctx),
            MessageTypes.GetUserPermissions => HandleGetUserPermissionsAsync(message, ctx),
            MessageTypes.SetUserPermission => HandleSetUserPermissionAsync(message, ctx),
            MessageTypes.SetUserRole => HandleSetUserRoleAsync(message, ctx),
            MessageTypes.GetChannelPermissions => HandleGetChannelPermissionsAsync(message, ctx),
            MessageTypes.SetChannelRolePermission => HandleSetChannelRolePermissionAsync(message, ctx),
            MessageTypes.SetChannelUserPermission => HandleSetChannelUserPermissionAsync(message, ctx),
            _ => Task.CompletedTask
        };
    }

    private async Task HandleListPermissionsAsync(ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var perms = auth.ListPermissions();
        var payload = new PermissionsListPayload
        {
            Permissions = perms.Select(p => new PermissionInfo { Id = p.Id, Name = p.Name, Description = p.Description }).ToList()
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.PermissionsList, payload);
        await ctx.Stream.WriteAsync(bytes, ctx.CancellationToken);
    }

    private async Task HandleListRolesAsync(ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var roles = auth.ListRoles();
        var payload = new RolesListPayload
        {
            Roles = roles.Select(r => new RoleInfo
            {
                Id = r.Id,
                Name = r.Name,
                Permissions = r.Permissions.Select(p => new RolePermissionAssignment
                {
                    PermissionId = p.PermissionId,
                    State = p.State switch { 0 => "allow", 1 => "deny", _ => "neutral" }
                }).ToList()
            }).ToList()
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.RolesList, payload);
        await ctx.Stream.WriteAsync(bytes, ctx.CancellationToken);
    }

    private async Task HandleGetUserPermissionsAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<GetUserPermissionsPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "UserId required");
            return;
        }
        var (perms, roleIds) = auth.GetUserPermissions(payload.UserId);
        var response = new UserPermissionsPayload
        {
            UserId = payload.UserId,
            Permissions = perms.Select(p => new UserPermissionAssignment
            {
                PermissionId = p.PermissionId,
                State = p.State switch { 0 => "allow", 1 => "deny", _ => "neutral" }
            }).ToList(),
            RoleIds = roleIds
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.UserPermissions, response);
        await ctx.Stream.WriteAsync(bytes, ctx.CancellationToken);
    }

    private async Task HandleSetUserPermissionAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<SetUserPermissionPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "UserId, PermissionId required");
            return;
        }
        int? stateVal = payload.State?.ToLowerInvariant() switch
        {
            "allow" => 0,
            "deny" => 1,
            "neutral" => 2,
            _ => null
        };
        auth.SetUserPermission(payload.UserId, payload.PermissionId, stateVal);
        var (perms, roleIds) = auth.GetUserPermissions(payload.UserId);
        var response = ControlProtocol.Serialize(MessageTypes.UserPermissions, new UserPermissionsPayload
        {
            UserId = payload.UserId,
            Permissions = perms.Select(p => new UserPermissionAssignment
            {
                PermissionId = p.PermissionId,
                State = p.State switch { 0 => "allow", 1 => "deny", _ => "neutral" }
            }).ToList(),
            RoleIds = roleIds
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    private async Task HandleSetUserRoleAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<SetUserRolePayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "UserId, RoleId required");
            return;
        }
        auth.SetUserRole(payload.UserId, payload.RoleId, payload.Assign);
        var (perms, roleIds) = auth.GetUserPermissions(payload.UserId);
        var response = ControlProtocol.Serialize(MessageTypes.UserPermissions, new UserPermissionsPayload
        {
            UserId = payload.UserId,
            Permissions = perms.Select(p => new UserPermissionAssignment
            {
                PermissionId = p.PermissionId,
                State = p.State switch { 0 => "allow", 1 => "deny", _ => "neutral" }
            }).ToList(),
            RoleIds = roleIds
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    private async Task HandleGetChannelPermissionsAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<GetChannelPermissionsPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "ChannelId required");
            return;
        }
        var (roleStates, userStates) = auth.GetChannelPermissions(payload.ChannelId);
        var response = new ChannelPermissionsPayload
        {
            ChannelId = payload.ChannelId,
            RoleStates = roleStates.Select(r => new ChannelRoleState
            {
                RoleId = r.RoleId,
                State = r.State switch { 0 => "allow", 1 => "deny", _ => "neutral" }
            }).ToList(),
            UserStates = userStates.Select(u => new ChannelUserState
            {
                UserId = u.UserId,
                State = u.State switch { 0 => "allow", 1 => "deny", _ => "neutral" }
            }).ToList()
        };
        var bytes = ControlProtocol.Serialize(MessageTypes.ChannelPermissions, response);
        await ctx.Stream.WriteAsync(bytes, ctx.CancellationToken);
    }

    private async Task HandleSetChannelRolePermissionAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<SetChannelRolePermissionPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "ChannelId, RoleId required");
            return;
        }
        int? stateVal = payload.State?.ToLowerInvariant() switch
        {
            "allow" => 0,
            "deny" => 1,
            "neutral" => 2,
            _ => null
        };
        auth.SetChannelRolePermission(payload.ChannelId, payload.RoleId, stateVal);
        var (roleStates, userStates) = auth.GetChannelPermissions(payload.ChannelId);
        var response = ControlProtocol.Serialize(MessageTypes.ChannelPermissions, new ChannelPermissionsPayload
        {
            ChannelId = payload.ChannelId,
            RoleStates = roleStates.Select(r => new ChannelRoleState { RoleId = r.RoleId, State = r.State switch { 0 => "allow", 1 => "deny", _ => "neutral" } }).ToList(),
            UserStates = userStates.Select(u => new ChannelUserState { UserId = u.UserId, State = u.State switch { 0 => "allow", 1 => "deny", _ => "neutral" } }).ToList()
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

    private async Task HandleSetChannelUserPermissionAsync(ControlMessage message, ControlHandlerContext ctx)
    {
        if (!await HandlerAuthHelper.RequireAdminAsync(auth, ctx))
            return;

        var payload = ControlProtocol.DeserializePayload<SetChannelUserPermissionPayload>(message);
        if (payload is null)
        {
            await ctx.SendErrorAsync("invalid_payload", "ChannelId, UserId required");
            return;
        }
        int? stateVal = payload.State?.ToLowerInvariant() switch
        {
            "allow" => 0,
            "deny" => 1,
            "neutral" => 2,
            _ => null
        };
        auth.SetChannelUserPermission(payload.ChannelId, payload.UserId, stateVal);
        var (roleStates, userStates) = auth.GetChannelPermissions(payload.ChannelId);
        var response = ControlProtocol.Serialize(MessageTypes.ChannelPermissions, new ChannelPermissionsPayload
        {
            ChannelId = payload.ChannelId,
            RoleStates = roleStates.Select(r => new ChannelRoleState { RoleId = r.RoleId, State = r.State switch { 0 => "allow", 1 => "deny", _ => "neutral" } }).ToList(),
            UserStates = userStates.Select(u => new ChannelUserState { UserId = u.UserId, State = u.State switch { 0 => "allow", 1 => "deny", _ => "neutral" } }).ToList()
        });
        await ctx.Stream.WriteAsync(response, ctx.CancellationToken);
    }

}
