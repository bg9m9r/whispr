namespace Whispr.Client.Services;

/// <summary>
/// Performs the connection, login, and initial server state fetch.
/// Injected for testability.
/// </summary>
public interface ILoginService
{
    /// <summary>
    /// Connects to the server, authenticates, and reads initial room state.
    /// </summary>
    Task<ConnectOutcome> ConnectAsync(ConnectParams parameters, CancellationToken ct = default);
}
