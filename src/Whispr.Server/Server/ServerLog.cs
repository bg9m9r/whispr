namespace Whispr.Server.Server;

/// <summary>
/// Simple timestamped console logging for server events.
/// </summary>
public static class ServerLog
{
    private static string Timestamp => DateTime.Now.ToString("HH:mm:ss");

    public static void Info(string message) =>
        Console.WriteLine($"[{Timestamp}] {message}");

    public static void Error(string message) =>
        Console.WriteLine($"[{Timestamp}] ERROR: {message}");
}
