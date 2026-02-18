using System.Text.Json;
using Whispr.Client.Models;

namespace Whispr.Client.Services;

/// <summary>
/// Persists server settings to JSON. Uses same directory as AudioSettings (~/.config/whispr).
/// </summary>
public sealed class ServerSettingsStore : IServerSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string GetSettingsPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "whispr");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "server-settings.json");
    }

    public ServerSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return ServerSettings.Default;

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? lastHost = null;
            var lastPort = 8443;
            var rememberMe = false;
            var servers = new List<ServerEntry>();

            if (root.TryGetProperty("lastServer", out var lastServer))
            {
                if (lastServer.TryGetProperty("host", out var h) && h.ValueKind == JsonValueKind.String)
                    lastHost = h.GetString();
                if (lastServer.TryGetProperty("port", out var p) && p.TryGetInt32(out var port))
                    lastPort = Math.Clamp(port, 1, 65535);
            }

            if (root.TryGetProperty("rememberMe", out var rm) && rm.ValueKind == JsonValueKind.True)
                rememberMe = true;

            if (root.TryGetProperty("servers", out var serversArr) && serversArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in serversArr.EnumerateArray())
                {
                    var host = item.TryGetProperty("host", out var sh) && sh.ValueKind == JsonValueKind.String ? sh.GetString() : "";
                    var port = item.TryGetProperty("port", out var sp) && sp.TryGetInt32(out var p) ? Math.Clamp(p, 1, 65535) : 8443;
                    var username = item.TryGetProperty("username", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "";
                    var rememberPassword = item.TryGetProperty("rememberPassword", out var rp) && rp.ValueKind == JsonValueKind.True;
                    if (!string.IsNullOrEmpty(host))
                        servers.Add(new ServerEntry(host, port, username, rememberPassword));
                }
            }

            return new ServerSettings(lastHost, lastPort, rememberMe, servers);
        }
        catch
        {
            return ServerSettings.Default;
        }
    }

    public void Save(ServerSettings settings)
    {
        try
        {
            var obj = new Dictionary<string, object?>
            {
                ["lastServer"] = new Dictionary<string, object?>
                {
                    ["host"] = settings.LastHost ?? "",
                    ["port"] = settings.LastPort
                },
                ["rememberMe"] = settings.RememberMe,
                ["servers"] = settings.Servers.Select(s => new Dictionary<string, object?>
                {
                    ["host"] = s.Host,
                    ["port"] = s.Port,
                    ["username"] = s.Username,
                    ["rememberPassword"] = s.RememberPassword
                }).ToList()
            };
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch
        {
            // ignore
        }
    }
}
