namespace Whispr.Server;

/// <summary>
/// Server configuration options.
/// </summary>
public sealed class ServerOptions
{
    public int ControlPort { get; init; } = 8443;
    public int AudioPort { get; init; } = 8444;
    public string CertificatePath { get; init; } = "cert.pfx";
    public string CertificatePassword { get; init; } = "";
    /// <summary>Path to SQLite database for users and ACL. Default: whispr.db in working directory.</summary>
    public string DatabasePath { get; init; } = "whispr.db";
}
