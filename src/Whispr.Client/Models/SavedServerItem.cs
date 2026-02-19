namespace Whispr.Client.Models;

/// <summary>
/// Display model for a saved server row (host:port (username)).
/// </summary>
public sealed record SavedServerItem(ServerEntry Entry)
{
    public string DisplayName => $"{Entry.Host}:{Entry.Port} ({Entry.Username})";
}
