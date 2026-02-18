using Whispr.Client.ViewModels;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// No-op implementation of ISettingsViewHost for unit testing.
/// Records calls for assertion if needed.
/// </summary>
public sealed class FakeSettingsViewHost : ISettingsViewHost
{
    public int MuteRoomAudioCallCount { get; private set; }
    public int UnmuteRoomAudioCallCount { get; private set; }
    public int RefreshLayoutCallCount { get; private set; }
    public int ShowSettingsBackCallCount { get; private set; }

    public void MuteRoomAudioForMicTest() => MuteRoomAudioCallCount++;

    public void UnmuteRoomAudioForMicTest() => UnmuteRoomAudioCallCount++;

    public void RefreshLayout() => RefreshLayoutCallCount++;

    public void ShowSettingsBack() => ShowSettingsBackCallCount++;
}
