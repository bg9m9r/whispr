namespace Whispr.Client;

/// <summary>
/// Simple client-side logging for debugging.
/// </summary>
public static class ClientLog
{
    private static string Timestamp => DateTime.Now.ToString("HH:mm:ss.fff");

    public static void Info(string message) =>
        Console.WriteLine($"[{Timestamp}] [Whispr] {message}");

    public static void Debug(string message) =>
        Console.WriteLine($"[{Timestamp}] [Whispr] [DEBUG] {message}");
}
