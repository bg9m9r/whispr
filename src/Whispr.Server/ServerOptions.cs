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

    /// <summary>
    /// When true and user store is empty, seeds admin/admin and bob/bob. For development only.
    /// Production should leave false and create admin via: whispr add-user &lt;username&gt; &lt;password&gt; --admin
    /// </summary>
    public bool SeedTestUsers { get; init; }

    /// <summary>
    /// Session token lifetime in hours. Default 24. Tokens expire after this duration.
    /// </summary>
    public int TokenLifetimeHours { get; init; } = 24;

    /// <summary>
    /// Base64-encoded 32-byte key for message encryption at rest. Set via WHISPR_MESSAGE_ENCRYPTION_KEY.
    /// When DatabasePath is set, required for message persistence; missing key causes startup to fail when using EF message repo.
    /// </summary>
    public string? MessageEncryptionKeyBase64 { get; init; }
}
