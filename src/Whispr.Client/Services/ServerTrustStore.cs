using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Whispr.Client.Services;

/// <summary>
/// Loads and provides certificate pins for known servers.
/// Config file: ~/.config/whispr/server-pins.json
/// Format: { "host:port": "base64sha256spki", ... }
/// </summary>
public static class ServerTrustStore
{
    private static string GetPinsPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "whispr");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "server-pins.json");
    }

    /// <summary>
    /// Gets the expected SPKI SHA256 hash (base64) for a server, or null if not pinned.
    /// </summary>
    public static string? GetPinnedHash(string host, int port)
    {
        try
        {
            var path = GetPinsPath();
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var key = $"{host}:{port}";
            if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Computes the base64-encoded SHA256 hash of the certificate's SPKI.
    /// Use this to generate pins for server-pins.json.
    /// </summary>
    public static string ComputeSpkiHash(X509Certificate certificate)
    {
        using var cert = new X509Certificate2(certificate);
        var spki = cert.PublicKey.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(spki);
        return Convert.ToBase64String(hash);
    }
}
