using Whispr.Client.Models;
using Whispr.Client.Services;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// In-memory IServerSettingsStore for unit testing.
/// </summary>
public sealed class FakeServerSettingsStore : IServerSettingsStore
{
    private ServerSettings _settings = ServerSettings.Default;

    public ServerSettings Load() => _settings;

    public void Save(ServerSettings settings) => _settings = settings;

    /// <summary>Set initial settings for tests.</summary>
    public void Set(ServerSettings settings) => _settings = settings;
}
