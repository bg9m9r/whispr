using Whispr.Client.Services;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// Configurable IAudioDeviceProvider for unit testing.
/// </summary>
public sealed class FakeAudioDeviceProvider : IAudioDeviceProvider
{
    public IReadOnlyList<string> CaptureDevices { get; set; } = [];
    public IReadOnlyList<string> PlaybackDevices { get; set; } = [];

    public IReadOnlyList<string> GetCaptureDevices(string? backend = null) => CaptureDevices;
    public IReadOnlyList<string> GetPlaybackDevices(string? backend = null) => PlaybackDevices;
}
