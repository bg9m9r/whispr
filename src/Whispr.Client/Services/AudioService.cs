using System.Net;
using System.Net.Sockets;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Enumeration;
using Silk.NET.OpenAL.Extensions.EXT;
using Whispr.Core.Crypto;
using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Captures mic, encodes with Opus, encrypts, sends via UDP; receives, decrypts, decodes, plays.
/// Uses OpenAL (cross-platform) for capture and playback.
/// </summary>
public sealed class AudioService : IDisposable
{
    private const int SampleRate = 48000;
    private const int FrameSamples = 960; // 20ms @ 48kHz
    private const int FrameBytes = FrameSamples * 2; // 16-bit

    private UdpClient? _udpClient;
    private Concentus.IOpusEncoder? _encoder;
    private Concentus.IOpusDecoder? _decoder;
    private byte[]? _audioKey;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private Task? _receiveTask;
    private bool _disposed;

    private AL? _al;
    private ALContext? _alc;
    private nint _playbackDevice;
    private nint _context;
    private uint _source;
    private uint[] _buffers = [];
    private readonly Queue<uint> _availableBuffers = new();
    private readonly object _playbackLock = new();
    private readonly Queue<byte[]> _playbackQueue = new();
    private const int NumBuffers = 8;
    private volatile bool _transmitting = true;
    private bool _voiceActivated;
    private int _micCutoffDelayMs;
    private CancellationTokenSource? _cutoffDelayCts;
    private double _noiseFloor = 5;
    private bool _gateOpen;
    private int _holdFramesRemaining;
    private double _openMargin = 12;
    private double _closeMargin = 4;
    private int _holdFrames = 12;
    private double _recentPeakWhileOpen;
    private const double CloseRatio = 0.15;
    private bool _mutedForMicTest;
    private bool _muteSend;
    private bool _muteReceive;
    private NoiseSuppressor? _noiseSuppressor;

    /// <summary>
    /// Raised when an audio frame is sent (parameter: peak level 0-100).
    /// </summary>
    public event Action<int>? OnFrameSent;

    /// <summary>
    /// Raised when a frame is captured (parameter: peak level 0-100). Fires even when not transmitting, for mic level display.
    /// </summary>
    public event Action<int>? OnCaptureLevel;

    /// <summary>
    /// Raised when an audio frame is received (parameter: sender's client ID).
    /// </summary>
    public event Action<uint>? OnFrameReceived;

    /// <summary>
    /// Raised when capture fails (e.g. no device, extension not available).
    /// </summary>
    public event Action<string>? OnCaptureFailed;

    /// <summary>
    /// Gets the list of available playback (output) devices. Returns empty if enumeration is not supported.
    /// On Linux, backend can be null (default), "pulse", or "alsa" to enumerate from that subsystem.
    /// </summary>
    public static IReadOnlyList<string> GetPlaybackDevices(string? backend = null)
    {
        var devices = new List<string>();
        try
        {
            unsafe
            {
                var alc = ALContext.GetApi();
                var device = OpenDeviceForBackend(alc, backend);
                if (device == null) return devices;

                if (alc.TryGetExtension<Enumeration>(device, out var enumeration))
                {
                    var list = enumeration.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers);
                    if (list is not null)
                        devices.AddRange(list);
                }
                alc.CloseDevice(device);
                alc.Dispose();
            }
        }
        catch { /* ignore */ }
        return devices;
    }

    /// <summary>
    /// Gets the list of available capture (microphone) devices. Returns empty if enumeration is not supported.
    /// On Linux, backend can be null (default), "pulse", or "alsa" to enumerate from that subsystem.
    /// </summary>
    public static IReadOnlyList<string> GetCaptureDevices(string? backend = null)
    {
        var devices = new List<string>();
        try
        {
            unsafe
            {
                var alc = ALContext.GetApi();
                var device = OpenDeviceForBackend(alc, backend);
                if (device == null) return devices;

                if (alc.TryGetExtension<CaptureEnumerationEnumeration>(device, out var captureEnum))
                {
                    var list = captureEnum.GetStringList(Silk.NET.OpenAL.Extensions.EXT.Enumeration.GetCaptureContextStringList.CaptureDeviceSpecifiers);
                    if (list is not null)
                        devices.AddRange(list);
                }
                alc.CloseDevice(device);
                alc.Dispose();
            }
        }
        catch { /* ignore */ }
        return devices;
    }

    private static unsafe Device* OpenDeviceForBackend(ALContext alc, string? backend)
    {
        if (string.IsNullOrEmpty(backend))
        {
            var d = alc.OpenDevice(null);
            if (d == null && OperatingSystem.IsLinux())
                d = alc.OpenDevice("pulse");
            return d;
        }
        var device = alc.OpenDevice(backend);
        if (device == null && OperatingSystem.IsLinux() && backend == "pulse")
            return alc.OpenDevice(null);
        return device;
    }

    /// <summary>
    /// When true, stops transmitting and playing received audio (used during mic test in Settings).
    /// </summary>
    public void SetMutedForMicTest(bool muted)
    {
        _mutedForMicTest = muted;
    }

    /// <summary>
    /// When true, stops transmitting audio. Use for testing receive-only.
    /// </summary>
    public void SetMuteSend(bool mute)
    {
        _muteSend = mute;
    }

    /// <summary>
    /// When true, stops playing received audio. Use for testing send-only.
    /// </summary>
    public void SetMuteReceive(bool mute)
    {
        _muteReceive = mute;
    }

    /// <summary>
    /// Sets whether audio is being transmitted (for push-to-talk). When false, transmission stops after MicCutoffDelayMs.
    /// </summary>
    public void SetTransmitting(bool transmitting)
    {
        _cutoffDelayCts?.Cancel();
        _cutoffDelayCts?.Dispose();
        _cutoffDelayCts = null;

        if (transmitting)
        {
            _transmitting = true;
            return;
        }

        if (_micCutoffDelayMs <= 0)
        {
            _transmitting = false;
            return;
        }

        _cutoffDelayCts = new CancellationTokenSource();
        var ct = _cutoffDelayCts.Token;
        var delay = _micCutoffDelayMs;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, ct);
                _transmitting = false;
            }
            catch (OperationCanceledException) { }
        }, ct);
    }

    /// <summary>
    /// Starts the audio pipeline: capture, encode, encrypt, send; receive, decrypt, decode, play.
    /// </summary>
    /// <param name="captureDeviceName">Capture device name from GetCaptureDevices(), or null for default.</param>
    /// <param name="playbackDeviceName">Playback device name from GetPlaybackDevices(), or null for default.</param>
    /// <param name="pushToTalk">When true, use Hold to Talk; when false with voiceActivated, use voice activation.</param>
    /// <param name="voiceActivated">When true and not pushToTalk, only transmit when voice is detected (noise gate).</param>
    /// <param name="micCutoffDelayMs">Delay in ms before stopping transmit after button release (push-to-talk). 0 = instant.</param>
    /// <param name="noiseSuppression">When true, apply RNNoise noise suppression before encoding.</param>
    /// <param name="noiseGateOpen">Level above noise floor to open gate (5-50, dB scale).</param>
    /// <param name="noiseGateClose">Level above noise floor to keep gate open (2-25, dB scale).</param>
    /// <param name="noiseGateHoldMs">Hold time in ms after level drops before closing gate.</param>
    public void Start(string serverHost, int audioPort, uint clientId, byte[] audioKey, string? captureDeviceName = null, string? playbackDeviceName = null, bool pushToTalk = false, bool voiceActivated = false, int micCutoffDelayMs = 200, bool noiseSuppression = false, int noiseGateOpen = 15, int noiseGateClose = 8, int noiseGateHoldMs = 240)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNotEqual(audioKey.Length, 32);

        _audioKey = (byte[])audioKey.Clone();
        _transmitting = !pushToTalk;
        _voiceActivated = voiceActivated && !pushToTalk;
        _micCutoffDelayMs = Math.Clamp(micCutoffDelayMs, 0, 1000);
        _noiseSuppressor?.Dispose();
        _noiseSuppressor = null;
        if (noiseSuppression)
        {
            try
            {
                _noiseSuppressor = new NoiseSuppressor();
            }
            catch (DllNotFoundException ex)
            {
                Console.WriteLine($"[Whispr] Noise suppression disabled: native library not found. On Linux install rnnoise (e.g. pacman -S rnnoise). {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Whispr] Noise suppression disabled: {ex.Message}");
            }
        }
        _openMargin = Math.Clamp(noiseGateOpen, 5, 50);
        _closeMargin = Math.Clamp(noiseGateClose, 2, 25);
        _holdFrames = Math.Max(0, noiseGateHoldMs / 20); // 20ms per frame
        _encoder = Concentus.OpusCodecFactory.CreateEncoder(SampleRate, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = 32000;
        _decoder = Concentus.OpusCodecFactory.CreateDecoder(SampleRate, 1);

        _udpClient = new UdpClient(0, System.Net.Sockets.AddressFamily.InterNetwork);
        var serverEndPoint = ResolveEndPoint(serverHost, audioPort);

        InitOpenAL(playbackDeviceName);
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SendEndpointRegistrationPacket(clientId, serverEndPoint);
        _captureTask = Task.Run(() => CaptureLoop(clientId, serverEndPoint, captureDeviceName, playbackDeviceName, ct));
        _receiveTask = Task.Run(() => ReceiveLoopAsync(ct));
    }

    /// <summary>
    /// Sends one packet so the server learns our UDP endpoint before we transmit.
    /// Without this, the server cannot relay to us until we send our first audio packet.
    /// </summary>
    private void SendEndpointRegistrationPacket(uint clientId, IPEndPoint serverEndPoint)
    {
        if (_encoder is null || _udpClient is null || _audioKey is null) return;

        var silentSamples = new short[FrameSamples];
        var opusBuffer = new byte[1275];
        var encodedLen = _encoder.Encode(silentSamples.AsSpan(), FrameSamples, opusBuffer.AsSpan(), opusBuffer.Length);
        if (encodedLen <= 0) return;

        var nonce = new byte[12];
        Random.Shared.NextBytes(nonce);
        var ciphertext = AudioEncryption.EncryptWithNonce(_audioKey, nonce, opusBuffer.AsSpan(0, encodedLen));
        var packet = AudioProtocol.BuildPacket(clientId, nonce, ciphertext);

        try
        {
            _udpClient.Send(packet, packet.Length, serverEndPoint);
            Console.WriteLine("[Whispr] Endpoint registration packet sent (server can now relay to us)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Whispr] Endpoint registration send failed: {ex.Message}");
        }
    }

    private static IPEndPoint ResolveEndPoint(string host, int port)
    {
        if (IPAddress.TryParse(host, out var ip))
            return new IPEndPoint(ip, port);
        var addresses = Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        var addr = ipv4 ?? addresses.FirstOrDefault() ?? throw new InvalidOperationException($"Could not resolve {host}");
        return new IPEndPoint(addr, port);
    }

    private unsafe void InitOpenAL(string? playbackDeviceName = null)
    {
        _alc = ALContext.GetApi();
        _al = AL.GetApi();

        var deviceName = string.IsNullOrWhiteSpace(playbackDeviceName) ? null : playbackDeviceName;
        var device = _alc.OpenDevice(deviceName);
        if (device == null)
            throw new InvalidOperationException("Failed to open OpenAL playback device. Ensure OpenAL/OpenAL Soft is installed (e.g. openal on Arch).");
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
    }

    private static int ComputePeakLevel(ReadOnlySpan<short> samples)
    {
        var peak = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var abs = Math.Abs((int)samples[i]);
            if (abs > peak) peak = abs;
        }
        return AudioLevelHelper.PeakToLevel(peak);
    }

    private static unsafe bool TryGetAvailableSamples(Capture capture, Device* device, out int available)
    {
        var arr = stackalloc int[1];
        capture.GetContextProperty(device, GetCaptureContextInteger.CaptureSamples, 1, arr);
        available = Math.Max(0, arr[0]);
        return available >= FrameSamples;
    }

    private unsafe void CaptureLoop(uint clientId, IPEndPoint serverEndPoint, string? captureDeviceName, string? playbackDeviceName, CancellationToken ct)
    {
        if (_encoder is null || _udpClient is null || _audioKey is null || _alc is null) return;

        var useDefault = string.IsNullOrWhiteSpace(playbackDeviceName);
        if (useDefault)
            CapturePrime.SpawnAndWait(playbackDeviceName, captureDeviceName);

        var playbackDevice = (Device*)_playbackDevice;
        if (!_alc.TryGetExtension<Capture>(playbackDevice, out var capture))
        {
            var msg = "Capture extension not available. Mic will not work.";
            Console.WriteLine($"Audio capture: {msg}");
            OnCaptureFailed?.Invoke(msg);
            return;
        }

        var bufferSize = FrameSamples * 30; // 600ms - larger buffer helps first-opener stability
        var deviceName = string.IsNullOrWhiteSpace(captureDeviceName) ? null : captureDeviceName;
        var device = capture.CaptureOpenDevice(deviceName, (uint)SampleRate, BufferFormat.Mono16, bufferSize);
        if (device == null && OperatingSystem.IsLinux())
            device = capture.CaptureOpenDevice("pulse", (uint)SampleRate, BufferFormat.Mono16, bufferSize);
        if (device == null && !string.IsNullOrWhiteSpace(captureDeviceName))
        {
            device = capture.CaptureOpenDevice(null, (uint)SampleRate, BufferFormat.Mono16, bufferSize);
            if (device != null)
                Console.WriteLine($"Audio capture: Selected device '{captureDeviceName}' could not be opened. Using system default.");
        }
        if (device == null)
        {
            var msg = $"Failed to open capture device '{captureDeviceName ?? "default"}'. On Linux, ensure PulseAudio is running (pulseaudio --start) or ALSA is configured.";
            Console.WriteLine($"Audio capture: {msg}");
            OnCaptureFailed?.Invoke(msg);
            return;
        }

        Console.WriteLine($"[Whispr] Audio capture started, device='{captureDeviceName ?? "default"}'");
        try
        {
            capture.CaptureStart(device);
            var buffer = new short[FrameSamples];

            // Warmup: discard first frames to let the device stabilize
            const int warmupFrames = 15;
            for (var w = 0; w < warmupFrames && !ct.IsCancellationRequested; w++)
            {
                while (!ct.IsCancellationRequested && !TryGetAvailableSamples(capture, device, out _))
                    Thread.Sleep(5);
                if (ct.IsCancellationRequested) break;
                fixed (short* p = buffer)
                    capture.CaptureSamples(device, p, FrameSamples);
            }
            if (warmupFrames > 0)
                Console.WriteLine($"[Whispr] Capture warm-up complete ({warmupFrames} frames discarded)");

            var consecutiveErrors = 0;
            const int maxConsecutiveErrors = 50;

            while (!ct.IsCancellationRequested && !_disposed)
            {
                try
                {
                    while (!ct.IsCancellationRequested && !TryGetAvailableSamples(capture, device, out _))
                        Thread.Sleep(5);
                    if (ct.IsCancellationRequested) break;

                    fixed (short* p = buffer)
                        capture.CaptureSamples(device, p, FrameSamples);

                    _noiseSuppressor?.Process(buffer.AsSpan());

                    var level = ComputePeakLevel(buffer.AsSpan());
                    OnCaptureLevel?.Invoke(level);
                    SendEncodedFrame(buffer, clientId, serverEndPoint);

                    consecutiveErrors = 0;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    Console.WriteLine($"[Whispr] Capture frame error #{consecutiveErrors}: {ex.Message}");
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        Console.WriteLine($"[Whispr] Capture stopped after {maxConsecutiveErrors} consecutive errors");
                        OnCaptureFailed?.Invoke($"Capture failed: {ex.Message}");
                        break;
                    }
                    Thread.Sleep(10); // Brief pause before retry to avoid tight error loop
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Whispr] Capture loop fatal error: {ex}");
            OnCaptureFailed?.Invoke($"Capture stopped: {ex.Message}");
        }
        finally
        {
            capture.CaptureStop(device);
            capture.CaptureCloseDevice(device);
        }
    }

    private int _sendCount;
    private int _receiveCount;

    private int _skipCount;

    /// <summary>
    /// Noise gate: only returns true when voice is detected. Uses adaptive threshold, hysteresis, hold time,
    /// and close threshold scaled by recent speaking level (so gate closes when level drops below ~25% of peak).
    /// Returns (send, fadeIn, fadeOut) for smooth gate transitions to reduce popping.
    /// </summary>
    private (bool send, bool fadeIn, bool fadeOut) ShouldTransmit(int level)
    {
        if (level < 2)
        {
            if (!_gateOpen)
                _noiseFloor = _noiseFloor * 0.995 + level * 0.005;
            return (false, false, false);
        }

        var openThreshold = _noiseFloor + _openMargin;
        var closeThreshold = Math.Max(_noiseFloor + _closeMargin, _recentPeakWhileOpen * CloseRatio);

        if (_gateOpen)
        {
            if (level >= closeThreshold)
            {
                _holdFramesRemaining = _holdFrames;
                _recentPeakWhileOpen = Math.Max(_recentPeakWhileOpen * 0.95, level);
                return (true, false, false);
            }
            if (_holdFramesRemaining > 0)
            {
                var remaining = _holdFramesRemaining;
                _holdFramesRemaining--;
                var fadeOut = remaining <= 2; // fade last 2 frames (~40ms) before gate closes
                return (true, false, fadeOut);
            }
            _gateOpen = false;
            _recentPeakWhileOpen = 0;
            return (false, false, false);
        }

        if (level >= openThreshold)
        {
            _gateOpen = true;
            _holdFramesRemaining = _holdFrames;
            _recentPeakWhileOpen = level;
            return (true, true, false);
        }

        if (!_gateOpen && level < 10)
            _noiseFloor = _noiseFloor * 0.99 + level * 0.01;

        return (false, false, false);
    }

    private static void ApplyFadeIn(Span<short> samples)
    {
        const int fadeSamples = 360; // ~7.5ms
        for (var i = 0; i < samples.Length && i < fadeSamples; i++)
        {
            var gain = (float)i / fadeSamples;
            samples[i] = (short)Math.Clamp(samples[i] * gain, -32768, 32767);
        }
    }

    private static void ApplyFadeOut(Span<short> samples)
    {
        const int fadeSamples = 480; // ~10ms per frame
        for (var i = 0; i < samples.Length; i++)
        {
            var distFromEnd = samples.Length - i;
            var gain = distFromEnd <= fadeSamples ? (float)distFromEnd / fadeSamples : 1f;
            samples[i] = (short)Math.Clamp(samples[i] * gain, -32768, 32767);
        }
    }

    private void SendEncodedFrame(short[] samples, uint clientId, IPEndPoint serverEndPoint)
    {
        if (_encoder is null || _udpClient is null || _audioKey is null) return;
        if (_mutedForMicTest || _muteSend) return;
        if (!_transmitting)
        {
            var n = System.Threading.Interlocked.Increment(ref _skipCount);
            if (n == 1 || n % 100 == 0)
                Console.WriteLine($"[Whispr] Skipped send #{n} (not transmitting - hold button to talk)");
            return;
        }

        var level = ComputePeakLevel(samples.AsSpan(0, FrameSamples));

        var (send, fadeIn, fadeOut) = _voiceActivated ? ShouldTransmit(level) : (true, false, false);
        if (!send)
            return;

        var span = samples.AsSpan(0, FrameSamples);
        if (fadeIn) ApplyFadeIn(span);
        if (fadeOut) ApplyFadeOut(span);

        var opusBuffer = new byte[1275];
        var encodedLen = _encoder.Encode(span, FrameSamples, opusBuffer.AsSpan(), opusBuffer.Length);
        if (encodedLen <= 0) return;

        var nonce = new byte[12];
        Random.Shared.NextBytes(nonce);
        var ciphertext = AudioEncryption.EncryptWithNonce(_audioKey, nonce, opusBuffer.AsSpan(0, encodedLen));
        var packet = AudioProtocol.BuildPacket(clientId, nonce, ciphertext);

        try
        {
            _udpClient.Send(packet, packet.Length, serverEndPoint);
            var n = System.Threading.Interlocked.Increment(ref _sendCount);
            if (n <= 3 || n % 50 == 0)
                Console.WriteLine($"[Whispr] Audio sent #{n} (level={level})");
            OnFrameSent?.Invoke(level);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Whispr] UDP send failed: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_udpClient is null || _decoder is null || _audioKey is null || _al is null) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                ProcessReceivedPacket(result.Buffer);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore */ }
        }
    }

    private void ProcessReceivedPacket(byte[] packet)
    {
        if (_decoder is null || _audioKey is null || _al is null) return;
        if (!AudioProtocol.TryParsePacket(packet, out var clientId, out var nonce, out var ciphertextWithTag)) return;

        try
        {
            var plaintext = AudioEncryption.DecryptWithKey(_audioKey, nonce, ciphertextWithTag);
            var samples = new short[FrameSamples];
            var decoded = _decoder.Decode(plaintext.AsSpan(), samples.AsSpan(), FrameSamples, false);
            if (decoded > 0)
            {
                if (!_mutedForMicTest && !_muteReceive)
                {
                    var bytes = decoded * 2;
                    var buffer = new byte[bytes];
                    Buffer.BlockCopy(samples, 0, buffer, 0, bytes);
                    lock (_playbackLock)
                        _playbackQueue.Enqueue(buffer);
                    FeedPlaybackBuffers();
                }
                var n = System.Threading.Interlocked.Increment(ref _receiveCount);
                if (n <= 3 || n % 50 == 0)
                    Console.WriteLine($"[Whispr] Audio received #{n}");
                OnFrameReceived?.Invoke(clientId);
            }
        }
        catch { /* ignore */ }
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

    public void Stop()
    {
        _cutoffDelayCts?.Cancel();
        _cutoffDelayCts?.Dispose();
        _cutoffDelayCts = null;
        _cts?.Cancel();
        _captureTask?.Wait(2000);
        _receiveTask?.Wait(2000);

        if (_al is not null && _source != 0)
        {
            _al.SourceStop(_source);
            _al.DeleteSource(_source);
            if (_buffers.Length > 0)
            {
                unsafe
                {
                    fixed (uint* p = _buffers)
                        _al.DeleteBuffers(_buffers.Length, p);
                }
            }
        }
        if (_alc is not null)
        {
            unsafe
            {
                if (_context != 0) _alc.DestroyContext((Context*)_context);
                if (_playbackDevice != 0) _alc.CloseDevice((Device*)_playbackDevice);
            }
        }
        _al?.Dispose();
        _alc?.Dispose();
        _al = null;
        _alc = null;

        _udpClient?.Dispose();
        _udpClient = null;
        _noiseSuppressor?.Dispose();
        _noiseSuppressor = null;
        _encoder = null;
        _decoder = null;
        if (_audioKey is not null)
        {
            Array.Clear(_audioKey, 0, _audioKey.Length);
            _audioKey = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
