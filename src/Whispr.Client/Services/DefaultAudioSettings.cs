namespace Whispr.Client.Services;

/// <summary>
/// Default implementation that delegates to static AudioSettings.
/// </summary>
public sealed class DefaultAudioSettings : IAudioSettings
{
    public (string? AudioBackend, string? CaptureDevice, string? PlaybackDevice, bool VoiceActivated, int MicCutoffDelayMs, bool NoiseSuppression, int NoiseGateOpen, int NoiseGateClose, int NoiseGateHoldMs) Load() =>
        AudioSettings.Load();

    public void Save(string? audioBackend = null, string? captureDevice = null, string? playbackDevice = null, bool voiceActivated = false, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240) =>
        AudioSettings.Save(audioBackend, captureDevice, playbackDevice, voiceActivated, micCutoffDelayMs, noiseSuppression, noiseGateOpen, noiseGateClose, noiseGateHoldMs);
}
