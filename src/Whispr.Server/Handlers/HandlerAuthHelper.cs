using Whispr.Server.Services;

namespace Whispr.Server.Handlers;

/// <summary>
/// Shared auth helpers for control message handlers.
/// </summary>
public static class HandlerAuthHelper
{
    /// <summary>
    /// Ensures user is logged in and token is valid. Sends error and returns false if not.
    /// </summary>
    public static async Task<bool> RequireAuthAsync(IAuthService auth, ControlHandlerContext ctx)
    {
        if (ctx.State.User is null || ctx.State.Token is null)
        {
            await ctx.SendErrorAsync("unauthorized", "Login required");
            return false;
        }
        if (auth.ValidateToken(ctx.State.Token) is null)
        {
            await ctx.SendErrorAsync("invalid_token", "Session expired");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Ensures user is logged in, token valid, and user is admin. Sends error and returns false if not.
    /// </summary>
    public static async Task<bool> RequireAdminAsync(IAuthService auth, ControlHandlerContext ctx)
    {
        if (!await RequireAuthAsync(auth, ctx))
            return false;
        if (!auth.IsAdmin(ctx.State.User!.Id))
        {
            await ctx.SendErrorAsync("forbidden", "Admin required");
            return false;
        }
        return true;
    }
}
