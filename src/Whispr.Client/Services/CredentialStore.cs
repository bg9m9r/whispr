using GitCredentialManager;

namespace Whispr.Client.Services;

/// <summary>
/// Cross-platform credential store using Git Credential Manager (Windows Credential Manager, macOS Keychain, Linux libsecret).
/// </summary>
public sealed class CredentialStore : ICredentialStore
{
    private readonly GitCredentialManager.ICredentialStore _gcmStore;

    public CredentialStore()
    {
        // On Linux, GCM defaults to no credential store (unset). Set secretservice so libsecret is used.
        if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCM_CREDENTIAL_STORE")))
            Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", "secretservice", EnvironmentVariableTarget.Process);

        _gcmStore = CredentialManager.Create("whispr");
    }

    public void Store(string service, string account, string password)
    {
        _gcmStore.AddOrUpdate(service, account, password);
    }

    public string? Retrieve(string service, string account)
    {
        var cred = _gcmStore.Get(service, account);
        return cred?.Password;
    }

    public void Remove(string service, string account)
    {
        _gcmStore.Remove(service, account);
    }
}
