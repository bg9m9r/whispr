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
    public string TransmitMode { get; set; } = "ptt";
    public int MicCutoffDelayMs { get; set; } = 200;
    public bool NoiseSuppression { get; set; }
    public int NoiseGateOpen { get; set; } = 15;
    public int NoiseGateClose { get; set; } = 8;
    public int NoiseGateHoldMs { get; set; } = 240;
    public string? PttKeyOrButton { get; set; } = "Key:V";

    public bool SaveCalled { get; private set; }

    public (string? AudioBackend, string? CaptureDevice, string? PlaybackDevice, string TransmitMode, int MicCutoffDelayMs, bool NoiseSuppression, int NoiseGateOpen, int NoiseGateClose, int NoiseGateHoldMs, string? PttKeyOrButton) Load() =>
        (AudioBackend, CaptureDevice, PlaybackDevice, TransmitMode, MicCutoffDelayMs, NoiseSuppression, NoiseGateOpen, NoiseGateClose, NoiseGateHoldMs, PttKeyOrButton);

    public void Save(string? audioBackend = null, string? captureDevice = null, string? playbackDevice = null, string? transmitMode = null, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240, string? pttKeyOrButton = null)
    {
        SaveCalled = true;
        AudioBackend = audioBackend;
        CaptureDevice = captureDevice;
        PlaybackDevice = playbackDevice;
        TransmitMode = transmitMode ?? "ptt";
        MicCutoffDelayMs = micCutoffDelayMs;
        NoiseSuppression = noiseSuppression;
        NoiseGateOpen = noiseGateOpen;
        NoiseGateClose = noiseGateClose;
        NoiseGateHoldMs = noiseGateHoldMs;
        PttKeyOrButton = pttKeyOrButton ?? "Key:V";
    }
}

