using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;
using Avalonia.Threading;

namespace Whispr.Client.Services;

/// <summary>
/// Captures from the selected microphone, reports level, and plays back audio for testing.
/// </summary>
public sealed class MicTestService : IDisposable
{
    private const int SampleRate = 48000;
    private const int FrameSamples = 960;
    private const int NumBuffers = 8;

    private ALContext? _alc;
    private AL? _al;
    private nint _playbackDevice;
    private nint _context;
    private uint _source;
    private uint[] _buffers = [];
    private readonly Queue<uint> _availableBuffers = new();
    private readonly Queue<byte[]> _playbackQueue = new();
    private readonly object _playbackLock = new();
    private static readonly byte[] SilenceBuffer = new byte[FrameSamples * 2];
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private bool _disposed;

    private double _noiseFloor = 5;
    private bool _gateOpen;
    private int _holdFramesRemaining;
    private double _openMargin = 15;
    private double _closeMargin = 8;
    private int _holdFrames = 12;
    private double _recentPeakWhileOpen;
    private bool _wasPlaying;
    private const double CloseRatio = 0.15;
    private NoiseSuppressor? _noiseSuppressor;
    private Concentus.IOpusEncoder? _encoder;
    private Concentus.IOpusDecoder? _decoder;

    /// <summary>
    /// Raised on the UI thread with the current mic level (0-100).
    /// </summary>
    public event Action<int>? OnLevel;

    /// <summary>
    /// Raised when capture fails.
    /// </summary>
    public event Action<string>? OnFailed;

    /// <param name="thresholdOpen">Level above noise to open gate (5-50).</param>
    /// <param name="thresholdClose">Level above noise to keep gate open (2-25).</param>
    /// <param name="holdMs">Hold time in ms after level drops.</param>
    /// <param name="noiseSuppression">When true, apply RNNoise before playback.</param>
    public void Start(string? captureDeviceName, string? playbackDeviceName = null, int thresholdOpen = 15, int thresholdClose = 8, int holdMs = 240, bool noiseSuppression = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Stop();

        _openMargin = Math.Clamp(thresholdOpen, 5, 50);
        _closeMargin = Math.Clamp(thresholdClose, 2, 25);
        _holdFrames = Math.Max(0, holdMs / 20);
        _noiseFloor = 5;
        _gateOpen = false;
        _holdFramesRemaining = 0;
        _recentPeakWhileOpen = 0;
        _wasPlaying = false;

        _noiseSuppressor?.Dispose();
        _noiseSuppressor = null;
        if (noiseSuppression)
        {
            try { _noiseSuppressor = new NoiseSuppressor(); }
            catch { /* ignore - test without suppression */ }
        }

        _encoder = Concentus.OpusCodecFactory.CreateEncoder(SampleRate, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = 32000;
        _decoder = Concentus.OpusCodecFactory.CreateDecoder(SampleRate, 1);

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _captureTask = Task.Run(() => CaptureLoop(captureDeviceName, playbackDeviceName, ct));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _captureTask?.Wait(2000);

        lock (_playbackLock)
        {
            _playbackQueue.Clear();
            _availableBuffers.Clear();
        }

        _noiseSuppressor?.Dispose();
        _noiseSuppressor = null;
        _encoder = null;
        _decoder = null;

        if (_al is not null && _source != 0)
        {
            unsafe
            {
                _al.SourceStop(_source);
                _al.DeleteSource(_source);
                _source = 0;
                if (_buffers.Length > 0)
                {
                    fixed (uint* p = _buffers)
                        _al.DeleteBuffers(_buffers.Length, p);
                }
                _buffers = [];
            }
            _al.Dispose();
            _al = null;
        }
        if (_alc is not null)
        {
            unsafe
            {
                if (_context != 0) _alc.DestroyContext((Context*)_context);
                if (_playbackDevice != 0)
                    _alc.CloseDevice((Device*)_playbackDevice);
            }
            _alc.Dispose();
            _alc = null;
            _context = 0;
            _playbackDevice = 0;
        }
    }

    private unsafe void CaptureLoop(string? captureDeviceName, string? playbackDeviceName, CancellationToken ct)
    {
        _alc = ALContext.GetApi();
        _al = AL.GetApi();
        var deviceName = string.IsNullOrWhiteSpace(playbackDeviceName) ? null : playbackDeviceName;
        var device = _alc.OpenDevice(deviceName);
        if (device == null && OperatingSystem.IsLinux())
        {
            device = _alc.OpenDevice("pulse");
            if (device == null)
                device = _alc.OpenDevice("alsa");
            if (device == null)
                device = _alc.OpenDevice(null);
        }
        if (device == null)
        {
            NotifyFailed("Failed to open OpenAL device. On Linux, ensure PulseAudio or PipeWire is running.");
            return;
        }
        _playbackDevice = (nint)device;

        var ctx = _alc.CreateContext(device, null);
        _alc.MakeContextCurrent(ctx);
        _context = (nint)ctx;

        _buffers = new uint[NumBuffers];
        fixed (uint* p = _buffers)
            _al.GenBuffers(NumBuffers, p);
        for (var i = 0; i < NumBuffers; i++)
            _availableBuffers.Enqueue(_buffers[i]);
        _source = _al.GenSource();

        if (!_alc.TryGetExtension<Capture>(device, out var capture))
        {
            NotifyFailed("Capture extension not available.");
            return;
        }

        var useDefault = string.IsNullOrWhiteSpace(playbackDeviceName);
        if (useDefault)
            CapturePrime.SpawnAndWait(playbackDeviceName, captureDeviceName);

        var bufferSize = FrameSamples * 30; // 600ms - larger buffer helps first-opener stability
        var capDeviceName = string.IsNullOrWhiteSpace(captureDeviceName) ? null : captureDeviceName;
        var capDevice = capture.CaptureOpenDevice(capDeviceName, (uint)SampleRate, BufferFormat.Mono16, bufferSize);
        if (capDevice == null && OperatingSystem.IsLinux())
        {
            capDevice = capture.CaptureOpenDevice("pulse", (uint)SampleRate, BufferFormat.Mono16, bufferSize);
            if (capDevice == null)
                capDevice = capture.CaptureOpenDevice("alsa", (uint)SampleRate, BufferFormat.Mono16, bufferSize);
            if (capDevice == null)
                capDevice = capture.CaptureOpenDevice(null, (uint)SampleRate, BufferFormat.Mono16, bufferSize);
        }
        if (capDevice == null && !string.IsNullOrWhiteSpace(captureDeviceName))
            capDevice = capture.CaptureOpenDevice(null, (uint)SampleRate, BufferFormat.Mono16, bufferSize);
        if (capDevice == null)
        {
            NotifyFailed($"Failed to open capture device '{captureDeviceName ?? "default"}'. Ensure PulseAudio is running.");
            return;
        }

        try
        {
            capture.CaptureStart(capDevice);
            var buffer = new short[FrameSamples];
            var availableArr = new int[1];

            const int warmupFrames = 15;
            for (var w = 0; w < warmupFrames && !ct.IsCancellationRequested; w++)
            {
                while (!ct.IsCancellationRequested)
                {
                    fixed (int* p = availableArr)
                        capture.GetContextProperty(capDevice, GetCaptureContextInteger.CaptureSamples, 1, p);
                    if (availableArr[0] >= FrameSamples) break;
                    Thread.Sleep(5);
                }
                if (ct.IsCancellationRequested) break;
                fixed (short* p = buffer)
                    capture.CaptureSamples(capDevice, p, FrameSamples);
            }

            while (!ct.IsCancellationRequested)
            {
                fixed (int* p = availableArr)
                    capture.GetContextProperty(capDevice, GetCaptureContextInteger.CaptureSamples, 1, p);
                if (availableArr[0] < FrameSamples)
                {
                    Thread.Sleep(5);
                    continue;
                }

                fixed (short* p = buffer)
                    capture.CaptureSamples(capDevice, p, FrameSamples);

                if (_noiseSuppressor != null)
                    _noiseSuppressor.Process(buffer.AsSpan());

                var peak = 0;
                for (var i = 0; i < FrameSamples; i++)
                {
                    var abs = Math.Abs((int)buffer[i]);
                    if (abs > peak) peak = abs;
                }
                var level = AudioLevelHelper.PeakToLevel(peak);
                Dispatcher.UIThread.Post(() => OnLevel?.Invoke(level));

                var shouldPlay = ShouldTransmit(level);
                byte[] pcm;
                if (shouldPlay)
                {
                    pcm = EncodeDecode(buffer);
                    if (pcm is not null)
                    {
                        if (!_wasPlaying)
                            ApplyFadeIn(pcm);
                        _wasPlaying = true;
                    }
                    else
                    {
                        pcm = SilenceBuffer;
                    }
                }
                else
                {
                    if (_wasPlaying)
                    {
                        var fadeBuffer = EncodeDecode(buffer);
                        if (fadeBuffer is not null)
                        {
                            ApplyFadeOut(fadeBuffer);
                            pcm = fadeBuffer;
                        }
                        else
                        {
                            pcm = SilenceBuffer;
                        }
                        _wasPlaying = false;
                    }
                    else
                    {
                        pcm = SilenceBuffer;
                    }
                }
                lock (_playbackLock)
                    _playbackQueue.Enqueue(pcm);
                FeedPlaybackBuffers();
            }
        }
        finally
        {
            capture.CaptureStop(capDevice);
            capture.CaptureCloseDevice(capDevice);
        }
    }

    private unsafe void FeedPlaybackBuffers()
    {
        if (_al is null) return;

        lock (_playbackLock)
        {
            _al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out int processed);
            for (var i = 0; i < processed; i++)
            {
                uint buf;
                _al.SourceUnqueueBuffers(_source, 1, &buf);
                _availableBuffers.Enqueue(buf);
            }

            while (_playbackQueue.Count > 0 && _availableBuffers.Count > 0)
            {
                var buf = _availableBuffers.Dequeue();
                var pcm = _playbackQueue.Dequeue();
                fixed (byte* p = pcm)
                    _al.BufferData(buf, BufferFormat.Mono16, p, pcm.Length, SampleRate);
                _al.SourceQueueBuffers(_source, [buf]);
            }

            _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int state);
            if (state != (int)SourceState.Playing && _availableBuffers.Count < NumBuffers)
                _al.SourcePlay(_source);
        }
    }

    private bool ShouldTransmit(int level)
    {
        if (level < 2)
        {
            if (!_gateOpen)
                _noiseFloor = _noiseFloor * 0.995 + level * 0.005;
            return false;
        }

        var openThreshold = _noiseFloor + _openMargin;
        var closeThreshold = Math.Max(_noiseFloor + _closeMargin, _recentPeakWhileOpen * CloseRatio);

        if (_gateOpen)
        {
            if (level >= closeThreshold)
            {
                _holdFramesRemaining = _holdFrames;
                _recentPeakWhileOpen = Math.Max(_recentPeakWhileOpen * 0.95, level);
                return true;
            }
            if (_holdFramesRemaining > 0)
            {
                _holdFramesRemaining--;
                return true;
            }
            _gateOpen = false;
            _recentPeakWhileOpen = 0;
            return false;
        }

        if (level >= openThreshold)
        {
            _gateOpen = true;
            _holdFramesRemaining = _holdFrames;
            _recentPeakWhileOpen = level;
            return true;
        }

        if (!_gateOpen && level < 10)
            _noiseFloor = _noiseFloor * 0.99 + level * 0.01;

        return false;
    }

    private byte[]? EncodeDecode(short[] buffer)
    {
        if (_encoder is null || _decoder is null) return null;
        var opusBuffer = new byte[1275];
        var encodedLen = _encoder.Encode(buffer.AsSpan(), FrameSamples, opusBuffer.AsSpan(), opusBuffer.Length);
        if (encodedLen <= 0) return null;
        var samples = new short[FrameSamples];
        var decoded = _decoder.Decode(opusBuffer.AsSpan(0, encodedLen), samples.AsSpan(), FrameSamples, false);
        if (decoded <= 0) return null;
        var pcm = new byte[FrameSamples * 2];
        Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
        return pcm;
    }

    private static void ApplyFadeIn(byte[] pcm)
    {
        const int fadeSamples = 120;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sampleIdx = i / 2;
            var gain = sampleIdx < fadeSamples ? (float)sampleIdx / fadeSamples : 1f;
            var s = (short)(pcm[i] | (pcm[i + 1] << 8));
            var faded = (short)Math.Clamp(s * gain, -32768, 32767);
            pcm[i] = (byte)faded;
            pcm[i + 1] = (byte)(faded >> 8);
        }
    }

    private static void ApplyFadeOut(byte[] pcm)
    {
        const int fadeSamples = 240;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sampleIdx = i / 2;
            var distFromEnd = FrameSamples - sampleIdx;
            var gain = distFromEnd <= fadeSamples ? (float)distFromEnd / fadeSamples : 1f;
            var s = (short)(pcm[i] | (pcm[i + 1] << 8));
            var faded = (short)Math.Clamp(s * gain, -32768, 32767);
            pcm[i] = (byte)faded;
            pcm[i + 1] = (byte)(faded >> 8);
        }
    }

    private void NotifyFailed(string msg)
    {
        Dispatcher.UIThread.Post(() => OnFailed?.Invoke(msg));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
