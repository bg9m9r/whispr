namespace Whispr.Client.Services;

/// <summary>
/// Secure storage for credentials (passwords). Uses OS keychain on each platform.
/// </summary>
public interface ICredentialStore
{
    void Store(string service, string account, string password);
    string? Retrieve(string service, string account);
    void Remove(string service, string account);
}
