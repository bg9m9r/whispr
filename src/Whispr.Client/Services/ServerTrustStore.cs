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

    private static string GetAcceptedUnverifiedPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "whispr");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "accepted-unverified.json");
    }

    /// <summary>
    /// True if the user previously chose to accept an unverified certificate for this server.
    /// </summary>
    public static bool IsAcceptedUnverified(string host, int port)
    {
        try
        {
            var path = GetAcceptedUnverifiedPath();
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var key = $"{host}:{port}";
            if (root.TryGetProperty("servers", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String && item.GetString() == key)
                        return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Records that the user accepted an unverified certificate for this server (e.g. "Save my decision").
    /// </summary>
    public static void AddAcceptedUnverified(string host, int port)
    {
        try
        {
            var path = GetAcceptedUnverifiedPath();
            var key = GetAcceptedUnverifiedKey(host, port);
            var servers = new List<string>();
            var skipped = new List<string>();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("servers", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var item in arr.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) servers.Add(item.GetString() ?? "");
                if (doc.RootElement.TryGetProperty("skippedDevWarning", out var darr) && darr.ValueKind == JsonValueKind.Array)
                    foreach (var item in darr.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) skipped.Add(item.GetString() ?? "");
            }
            if (servers.Contains(key)) return;
            servers.Add(key);
            var obj = new Dictionary<string, object> { ["servers"] = servers, ["skippedDevWarning"] = skipped };
            File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Clears all saved cert decisions: "accept unverified" and "skip dev warning" (e.g. when user clears saved credentials).
    /// </summary>
    public static void ClearAcceptedUnverified()
    {
        try
        {
            var path = GetAcceptedUnverifiedPath();
            File.WriteAllText(path, """{"servers":[],"skippedDevWarning":[]}""");
        }
        catch
        {
            // ignore
        }
    }

    private static string GetAcceptedUnverifiedKey(string host, int port) => $"{host}:{port}";

    /// <summary>
    /// True if the user previously chose to skip the dev/untrusted cert warning for this server (e.g. localhost).
    /// </summary>
    public static bool IsSkippedDevCertWarning(string host, int port)
    {
        try
        {
            var path = GetAcceptedUnverifiedPath();
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var key = GetAcceptedUnverifiedKey(host, port);
            if (doc.RootElement.TryGetProperty("skippedDevWarning", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var item in arr.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String && item.GetString() == key)
                        return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Records that the user chose to skip the dev cert warning for this server.
    /// </summary>
    public static void AddSkippedDevCertWarning(string host, int port)
    {
        try
        {
            var path = GetAcceptedUnverifiedPath();
            var key = GetAcceptedUnverifiedKey(host, port);
            var servers = new List<string>();
            var skipped = new List<string>();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("servers", out var sarr) && sarr.ValueKind == JsonValueKind.Array)
                    foreach (var item in sarr.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) servers.Add(item.GetString() ?? "");
                if (doc.RootElement.TryGetProperty("skippedDevWarning", out var darr) && darr.ValueKind == JsonValueKind.Array)
                    foreach (var item in darr.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) skipped.Add(item.GetString() ?? "");
            }
            if (skipped.Contains(key)) return;
            skipped.Add(key);
            var obj = new Dictionary<string, object> { ["servers"] = servers, ["skippedDevWarning"] = skipped };
            File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // ignore
        }
    }
}
