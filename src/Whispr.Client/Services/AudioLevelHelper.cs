namespace Whispr.Client.Services;

/// <summary>
/// Converts raw PCM peak to a 0-100 level using a logarithmic (dB) scale.
/// Typical speech at -20dB shows ~67 instead of ~10 with linear scale.
/// </summary>
public static class AudioLevelHelper
{
    /// <summary>
    /// Converts peak amplitude (0-32767) to level 0-100 using dB scale.
    /// -60dB = 0, 0dB (full scale) = 100.
    /// </summary>
    public static int PeakToLevel(int peak)
    {
        if (peak <= 0) return 0;
        var dB = 20 * Math.Log10((double)peak / 32767);
        var level = (dB + 60) / 60 * 100;
        return (int)Math.Clamp(level, 0, 100);
    }
}
