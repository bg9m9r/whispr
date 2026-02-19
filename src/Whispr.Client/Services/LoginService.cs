using System.Security.Authentication;
using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Performs connection, login, and initial server state fetch.
/// </summary>
public sealed class LoginService : ILoginService
{
    public async Task<ConnectOutcome> ConnectAsync(ConnectParams parameters, CancellationToken ct = default)
    {
        var connection = new ConnectionService();
        try
        {
            await connection.ConnectAsync(
                parameters.Host,
                parameters.Port,
                parameters.AllowDevCert,
                parameters.AcceptUnverifiedCert,
                parameters.PinnedCertHash,
                ct);

            var auth = new AuthService(connection);
            var loginResult = await auth.LoginAsync(parameters.Username, parameters.Password, ct);

            if (!loginResult.Success)
            {
                connection.Dispose();
                return new ConnectFailed(loginResult.Error ?? "Login failed");
            }

            var serverState = await auth.ReadInitialServerStateAsync(ct);
            if (serverState is null)
            {
                connection.Dispose();
                return new ConnectFailed("Failed to join default channel");
            }

            return new ConnectSuccess(connection, auth, serverState.Value.ChannelJoined, serverState.Value.ServerState);
        }
        catch (Exception ex)
        {
            connection.Dispose();
            var isCertError = IsCertificateValidationFailure(ex);
            return new ConnectFailed(ex.Message, isCertError);
        }
    }

    private static bool IsCertificateValidationFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is AuthenticationException)
                return true;
            var msg = e.Message;
            if (msg.Contains("remote certificate", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("certificate", StringComparison.OrdinalIgnoreCase) && msg.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("TLS", StringComparison.OrdinalIgnoreCase) && msg.Contains("authenticate", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
