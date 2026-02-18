namespace Whispr.Client.Models;

/// <summary>
/// Saved server connection details (non-sensitive). Password is stored separately in keychain.
/// </summary>
public sealed record ServerEntry(string Host, int Port, string Username, bool RememberPassword);
