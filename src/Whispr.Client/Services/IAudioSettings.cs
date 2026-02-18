namespace Whispr.Client.Services;

/// <summary>
/// Loads and saves audio device preferences. Injected for testability.
/// </summary>
public interface IAudioSettings
{
    (string? AudioBackend, string? CaptureDevice, string? PlaybackDevice, bool VoiceActivated, int MicCutoffDelayMs, bool NoiseSuppression, int NoiseGateOpen, int NoiseGateClose, int NoiseGateHoldMs) Load();

    void Save(string? audioBackend = null, string? captureDevice = null, string? playbackDevice = null, bool voiceActivated = false, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240);
}
