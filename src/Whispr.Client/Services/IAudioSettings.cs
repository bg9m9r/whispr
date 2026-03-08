namespace Whispr.Client.Services;

/// <summary>
/// Loads and saves audio device preferences. Injected for testability.
/// </summary>
public interface IAudioSettings
{
    /// <summary>TransmitMode: "voice" (voice activation), "ptt" (push-to-talk), "open" (always transmit).</summary>
    (string? AudioBackend, string? CaptureDevice, string? PlaybackDevice, string TransmitMode, int MicCutoffDelayMs, bool NoiseSuppression, int NoiseGateOpen, int NoiseGateClose, int NoiseGateHoldMs, string? PttKeyOrButton) Load();

    void Save(string? audioBackend = null, string? captureDevice = null, string? playbackDevice = null, string? transmitMode = null, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240, string? pttKeyOrButton = null);
}
