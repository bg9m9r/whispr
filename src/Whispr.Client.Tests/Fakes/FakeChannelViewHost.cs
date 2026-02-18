using Whispr.Client.ViewModels;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// No-op implementation of IChannelViewHost for unit testing.
/// Records calls for assertion if needed.
/// </summary>
public sealed class FakeChannelViewHost : IChannelViewHost
{
    public int ShowSettingsCallCount { get; private set; }
    public int ShowLoginCallCount { get; private set; }
    public int ShowPermissionsWindowCallCount { get; private set; }
    public int ShowChannelPermissionsWindowCallCount { get; private set; }
    public int RestartAudioCallCount { get; private set; }

    public Guid? LastPermissionsUserId { get; private set; }
    public string? LastPermissionsUsername { get; private set; }
    public Guid? LastChannelPermissionsChannelId { get; private set; }
    public string? LastChannelPermissionsChannelName { get; private set; }

    public void ShowSettings() => ShowSettingsCallCount++;

    public void ShowLogin() => ShowLoginCallCount++;

    public Task ShowPermissionsWindowAsync(Guid userId, string username)
    {
        ShowPermissionsWindowCallCount++;
        LastPermissionsUserId = userId;
        LastPermissionsUsername = username;
        return Task.CompletedTask;
    }

    public Task ShowChannelPermissionsWindowAsync(Guid channelId, string channelName)
    {
        ShowChannelPermissionsWindowCallCount++;
        LastChannelPermissionsChannelId = channelId;
        LastChannelPermissionsChannelName = channelName;
        return Task.CompletedTask;
    }

    public void RestartAudioWithNewSettings() => RestartAudioCallCount++;
}
