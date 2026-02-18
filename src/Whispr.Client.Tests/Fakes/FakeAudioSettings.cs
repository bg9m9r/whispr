using Whispr.Client.Services;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// In-memory IAudioSettings for unit testing.
/// </summary>
public sealed class FakeAudioSettings : IAudioSettings
{
    public string? AudioBackend { get; set; }
    public string? CaptureDevice { get; set; }
    public string? PlaybackDevice { get; set; }
    public bool VoiceActivated { get; set; }
    public int MicCutoffDelayMs { get; set; } = 200;
    public bool NoiseSuppression { get; set; }
    public int NoiseGateOpen { get; set; } = 15;
    public int NoiseGateClose { get; set; } = 8;
    public int NoiseGateHoldMs { get; set; } = 240;

    public bool SaveCalled { get; private set; }

    public (string? AudioBackend, string? CaptureDevice, string? PlaybackDevice, bool VoiceActivated, int MicCutoffDelayMs, bool NoiseSuppression, int NoiseGateOpen, int NoiseGateClose, int NoiseGateHoldMs) Load() =>
        (AudioBackend, CaptureDevice, PlaybackDevice, VoiceActivated, MicCutoffDelayMs, NoiseSuppression, NoiseGateOpen, NoiseGateClose, NoiseGateHoldMs);

    public void Save(string? audioBackend = null, string? captureDevice = null, string? playbackDevice = null, bool voiceActivated = false, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240)
    {
        SaveCalled = true;
        AudioBackend = audioBackend;
        CaptureDevice = captureDevice;
        PlaybackDevice = playbackDevice;
        VoiceActivated = voiceActivated;
        MicCutoffDelayMs = micCutoffDelayMs;
        NoiseSuppression = noiseSuppression;
        NoiseGateOpen = noiseGateOpen;
        NoiseGateClose = noiseGateClose;
        NoiseGateHoldMs = noiseGateHoldMs;
    }
}
