using System.Text.Json;

namespace Whispr.Client.Services;

/// <summary>
/// Persists audio device preferences to a local JSON file.
/// </summary>
public static class AudioSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string GetSettingsPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "whispr");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "audio-settings.json");
    }

    /// <summary>
    /// Loads saved audio settings. Returns defaults if file doesn't exist.
    /// audioBackend: null = system default, "pulse" = PulseAudio, "alsa" = ALSA (Linux only).
    /// </summary>
    public static (string? AudioBackend, string? CaptureDevice, string? PlaybackDevice, bool VoiceActivated, int MicCutoffDelayMs, bool NoiseSuppression, int NoiseGateOpen, int NoiseGateClose, int NoiseGateHoldMs) Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return (null, null, null, false, 200, false, 15, 8, 240);

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var audioBackend = root.TryGetProperty("audioBackend", out var ab) && ab.ValueKind == JsonValueKind.String
                ? ab.GetString()
                : null;
            var capture = root.TryGetProperty("captureDevice", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            var playback = root.TryGetProperty("playbackDevice", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : null;
            if (string.IsNullOrEmpty(audioBackend) && (capture == "pulse" || capture == "alsa" || playback == "pulse" || playback == "alsa"))
            {
                audioBackend = capture == "pulse" || capture == "alsa" ? capture : playback;
                if (capture == "pulse" || capture == "alsa") capture = null;
                if (playback == "pulse" || playback == "alsa") playback = null;
            }
            var voiceActivated = root.TryGetProperty("voiceActivated", out var v) && v.ValueKind == JsonValueKind.True;
            var micCutoffDelayMs = root.TryGetProperty("micCutoffDelayMs", out var d) && d.TryGetInt32(out var delay)
                ? Math.Clamp(delay, 0, 1000)
                : 200;
            var noiseSuppression = root.TryGetProperty("noiseSuppression", out var n) && n.ValueKind == JsonValueKind.True;
            var noiseGateOpen = root.TryGetProperty("noiseGateOpen", out var go) && go.TryGetInt32(out var open)
                ? Math.Clamp(open, 5, 50)
                : 15;
            var noiseGateClose = root.TryGetProperty("noiseGateClose", out var gc) && gc.TryGetInt32(out var close)
                ? Math.Clamp(close, 2, 25)
                : 8;
            var noiseGateHoldMs = root.TryGetProperty("noiseGateHoldMs", out var gh) && gh.TryGetInt32(out var hold)
                ? Math.Clamp(hold, 0, 500)
                : 240;

            return (string.IsNullOrEmpty(audioBackend) ? null : audioBackend, string.IsNullOrEmpty(capture) ? null : capture, string.IsNullOrEmpty(playback) ? null : playback, voiceActivated, micCutoffDelayMs, noiseSuppression, noiseGateOpen, noiseGateClose, noiseGateHoldMs);
        }
        catch
        {
            return (null, null, null, false, 200, false, 15, 8, 240);
        }
    }

    /// <summary>
    /// Saves audio device preferences.
    /// </summary>
    public static void Save(string? audioBackend = null, string? captureDevice = null, string? playbackDevice = null, bool voiceActivated = false, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240)
    {
        try
        {
            var obj = new Dictionary<string, object?>
            {
                ["audioBackend"] = string.IsNullOrEmpty(audioBackend) ? null : audioBackend,
                ["captureDevice"] = string.IsNullOrEmpty(captureDevice) ? null : captureDevice,
                ["playbackDevice"] = string.IsNullOrEmpty(playbackDevice) ? null : playbackDevice,
                ["voiceActivated"] = voiceActivated,
                ["micCutoffDelayMs"] = Math.Clamp(micCutoffDelayMs, 0, 1000),
                ["noiseSuppression"] = noiseSuppression,
                ["noiseGateOpen"] = Math.Clamp(noiseGateOpen, 5, 50),
                ["noiseGateClose"] = Math.Clamp(noiseGateClose, 2, 25),
                ["noiseGateHoldMs"] = Math.Clamp(noiseGateHoldMs, 0, 500)
            };
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch
        {
            // ignore
        }
    }
}
