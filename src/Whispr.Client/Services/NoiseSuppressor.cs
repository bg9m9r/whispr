using RNNoise.NET;

namespace Whispr.Client.Services;

/// <summary>
/// Wraps RNNoise for real-time noise suppression. Converts between 16-bit PCM and float,
/// processes in 480-sample frames (RNNoise requirement).
/// </summary>
public sealed class NoiseSuppressor : IDisposable
{
    private const int RnnoiseFrameSize = 480;
    private readonly Denoiser _denoiser;
    private readonly float[] _floatBuffer;
    private bool _disposed;

    public NoiseSuppressor()
    {
        _denoiser = new Denoiser();
        _floatBuffer = new float[RnnoiseFrameSize];
    }

    /// <summary>
    /// Processes 960 samples (20ms @ 48kHz) in-place. Applies RNNoise noise suppression.
    /// </summary>
    public void Process(Span<short> samples)
    {
        if (samples.Length != 960) return;

        for (var chunk = 0; chunk < 2; chunk++)
        {
            var offset = chunk * RnnoiseFrameSize;
            for (var i = 0; i < RnnoiseFrameSize; i++)
                _floatBuffer[i] = samples[offset + i] / 32768f;

            var span = _floatBuffer.AsSpan();
            _denoiser.Denoise(span, finish: true);

            for (var i = 0; i < RnnoiseFrameSize; i++)
            {
                var s = (int)(_floatBuffer[i] * 32767f);
                samples[offset + i] = (short)Math.Clamp(s, short.MinValue, short.MaxValue);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _denoiser.Dispose();
        _disposed = true;
    }
}
