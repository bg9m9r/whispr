namespace Whispr.Client.Services;

/// <summary>
/// Parameters for connecting to the server.
/// </summary>
public sealed record ConnectParams(
    string Host,
    int Port,
    string Username,
    string Password,
    bool AllowDevCert,
    string? PinnedCertHash,
    bool AcceptUnverifiedCert);
