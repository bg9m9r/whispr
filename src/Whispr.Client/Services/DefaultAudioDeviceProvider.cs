namespace Whispr.Client.Services;

/// <summary>
/// Default implementation that delegates to AudioService static methods.
/// </summary>
public sealed class DefaultAudioDeviceProvider : IAudioDeviceProvider
{
    public IReadOnlyList<string> GetCaptureDevices(string? backend = null) =>
        AudioService.GetCaptureDevices(backend);

    public IReadOnlyList<string> GetPlaybackDevices(string? backend = null) =>
        AudioService.GetPlaybackDevices(backend);
}
