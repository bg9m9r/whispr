namespace Whispr.Client.Services;

/// <summary>
/// Provides capture and playback device lists. Injected for testability.
/// </summary>
public interface IAudioDeviceProvider
{
    IReadOnlyList<string> GetCaptureDevices(string? backend = null);
    IReadOnlyList<string> GetPlaybackDevices(string? backend = null);
}
