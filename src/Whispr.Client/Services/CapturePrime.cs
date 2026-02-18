using System.Diagnostics;
using System.Reflection;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;

namespace Whispr.Client.Services;

/// <summary>
/// Primes the capture device by opening it briefly in a separate process.
/// On Linux, the first process to open the capture device often gets worse quality;
/// spawning a prime process first makes our real capture the "second" opener.
/// </summary>
public static class CapturePrime
{
    private const int SampleRate = 48000;
    private const int FrameSamples = 960;
    private const int PrimeSeconds = 60; // Hold device open so we stay the "second" opener
    private const int BufferSize = FrameSamples * 30;

    /// <summary>
    /// Runs the capture prime in this process. Call when invoked with --prime-capture.
    /// playbackDeviceName: backend ("pulse"/"alsa") or null for default. captureDeviceName: device or null for default from backend.
    /// </summary>
    public static unsafe int Run(string? playbackDeviceName, string? captureDeviceName)
    {
        try
        {
            var alc = ALContext.GetApi();
            var device = alc.OpenDevice(string.IsNullOrWhiteSpace(playbackDeviceName) ? null : playbackDeviceName);
            if (device == null && OperatingSystem.IsLinux())
            {
                device = alc.OpenDevice("pulse");
                if (device == null)
                    device = alc.OpenDevice("alsa");
                if (device == null)
                    device = alc.OpenDevice(null);
            }
            if (device == null)
            {
                Console.WriteLine("[Whispr] Capture prime: failed to open playback device");
                return 1;
            }

            if (!alc.TryGetExtension<Capture>(device, out var capture))
            {
                alc.CloseDevice(device);
                alc.Dispose();
                Console.WriteLine("[Whispr] Capture prime: Capture extension not available");
                return 1;
            }

            var capDeviceName = string.IsNullOrWhiteSpace(captureDeviceName) ? null : captureDeviceName;
            var capDevice = capture.CaptureOpenDevice(capDeviceName, (uint)SampleRate, BufferFormat.Mono16, BufferSize);
            if (capDevice == null && OperatingSystem.IsLinux())
            {
                capDevice = capture.CaptureOpenDevice("pulse", (uint)SampleRate, BufferFormat.Mono16, BufferSize);
                if (capDevice == null)
                    capDevice = capture.CaptureOpenDevice("alsa", (uint)SampleRate, BufferFormat.Mono16, BufferSize);
                if (capDevice == null)
                    capDevice = capture.CaptureOpenDevice(null, (uint)SampleRate, BufferFormat.Mono16, BufferSize);
            }
            if (capDevice == null)
            {
                alc.CloseDevice(device);
                alc.Dispose();
                Console.WriteLine("[Whispr] Capture prime: failed to open capture device");
                return 1;
            }

            capture.CaptureStart(capDevice);
            var buffer = new short[FrameSamples];
            var availableArr = new int[1];
            var frameCount = PrimeSeconds * (SampleRate / FrameSamples); // 50 fps

            for (var i = 0; i < frameCount; i++)
            {
                while (true)
                {
                    fixed (int* p = availableArr)
                        capture.GetContextProperty(capDevice, GetCaptureContextInteger.CaptureSamples, 1, p);
                    if (availableArr[0] >= FrameSamples) break;
                    Thread.Sleep(5);
                }
                fixed (short* p = buffer)
                    capture.CaptureSamples(capDevice, p, FrameSamples);
            }

            capture.CaptureStop(capDevice);
            capture.CaptureCloseDevice(capDevice);
            alc.CloseDevice(device);
            alc.Dispose();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Whispr] Capture prime failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Spawns a short-lived process that primes the capture device, then waits.
    /// Call before starting real capture to improve quality when we're the first instance.
    /// </summary>
    public static void SpawnAndWait(string? playbackDeviceName, string? captureDeviceName)
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            var dll = Assembly.GetExecutingAssembly().Location;
            var playbackArg = string.IsNullOrWhiteSpace(playbackDeviceName) ? "" : $" \"{playbackDeviceName.Replace("\"", "\\\"")}\"";
            var captureArg = string.IsNullOrWhiteSpace(captureDeviceName) ? "" : $" \"{captureDeviceName.Replace("\"", "\\\"")}\"";
            var args = string.IsNullOrEmpty(dll)
                ? $"--prime-capture{playbackArg}{captureArg}"
                : $"\"{dll}\" --prime-capture{playbackArg}{captureArg}";

            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? "dotnet",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            Thread.Sleep(800); // Let prime open the device before we open
        }
        catch
        {
            // Ignore - prime is best-effort
        }
    }
}
