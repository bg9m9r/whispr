using System.Reflection;

namespace Whispr.Core;

/// <summary>
/// Helpers for reading and comparing application versions.
/// </summary>
public static class VersionHelper
{
    /// <summary>
    /// Gets the informational version from the assembly (set by MSBuild Version property).
    /// </summary>
    public static string GetVersion(Assembly assembly)
    {
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attr?.InformationalVersion ?? "0.0.0";
    }

    /// <summary>
    /// Parses a semantic version string (e.g. "1.2.3") and returns (major, minor, patch).
    /// Returns (0,0,0) for invalid or null input.
    /// </summary>
    public static (int Major, int Minor, int Patch) Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return (0, 0, 0);

        var parts = version.Trim().Split('.');
        if (parts.Length < 1)
            return (0, 0, 0);

        int major = 0, minor = 0, patch = 0;
        if (parts.Length >= 1 && int.TryParse(parts[0], out var m))
            major = m;
        if (parts.Length >= 2 && int.TryParse(parts[1], out var n))
            minor = n;
        if (parts.Length >= 3 && int.TryParse(parts[2].Split('-', '+')[0], out var p))
            patch = p;

        return (major, minor, patch);
    }

    /// <summary>
    /// Returns true if <paramref name="client"/> is greater than or equal to <paramref name="minRequired"/>.
    /// </summary>
    public static bool IsAtLeast(string? client, string? minRequired)
    {
        if (string.IsNullOrWhiteSpace(minRequired))
            return true;
        if (string.IsNullOrWhiteSpace(client))
            return false;

        var c = Parse(client);
        var m = Parse(minRequired);

        if (c.Major != m.Major) return c.Major > m.Major;
        if (c.Minor != m.Minor) return c.Minor > m.Minor;
        return c.Patch >= m.Patch;
    }
}
