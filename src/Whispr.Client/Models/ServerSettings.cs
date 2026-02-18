namespace Whispr.Client.Models;

/// <summary>
/// Non-sensitive server preferences persisted to JSON.
/// </summary>
public sealed record ServerSettings(
    string? LastHost,
    int LastPort,
    bool RememberMe,
    IReadOnlyList<ServerEntry> Servers)
{
    public static ServerSettings Default { get; } = new(null, 8443, false, []);
}
