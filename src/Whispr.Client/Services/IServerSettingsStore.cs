using Whispr.Client.Models;

namespace Whispr.Client.Services;

/// <summary>
/// Loads and saves non-sensitive server preferences (host, port, username, remember flags).
/// </summary>
public interface IServerSettingsStore
{
    ServerSettings Load();
    void Save(ServerSettings settings);
}
