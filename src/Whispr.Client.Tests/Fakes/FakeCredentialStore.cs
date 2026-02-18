using System.Collections.Generic;
using Whispr.Client.Services;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// In-memory ICredentialStore for unit testing.
/// </summary>
public sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<(string Service, string Account), string> _store = new();

    public void Store(string service, string account, string password)
    {
        _store[(service, account)] = password;
    }

    public string? Retrieve(string service, string account)
    {
        return _store.TryGetValue((service, account), out var pwd) ? pwd : null;
    }

    public void Remove(string service, string account)
    {
        _store.Remove((service, account));
    }

    /// <summary>Number of stored credentials.</summary>
    public int Count => _store.Count;
}
