using Avalonia;
using Whispr.Client.Services;

namespace Whispr.Client;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var primeIdx = Array.IndexOf(args, "--prime-capture");
        if (primeIdx >= 0)
        {
            var playback = primeIdx + 1 < args.Length ? args[primeIdx + 1] : null;
            var capture = primeIdx + 2 < args.Length ? args[primeIdx + 2] : null;
            Environment.Exit(CapturePrime.Run(playback, capture));
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
