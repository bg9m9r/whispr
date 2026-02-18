using System.Collections.Concurrent;
using Whispr.Core.Models;

namespace Whispr.Server.Repositories;

/// <summary>
/// In-memory user repository for development/testing when no database is configured.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _byUsername = new();
    private readonly ConcurrentDictionary<Guid, User> _byId = new();

    public IReadOnlyList<User> LoadAll() => _byId.Values.ToList();

    public User? GetByUsername(string username) =>
        _byUsername.TryGetValue(username, out var u) ? u : null;

    public User? GetById(Guid id) =>
        _byId.TryGetValue(id, out var u) ? u : null;

    public bool Insert(User user)
    {
        if (_byUsername.ContainsKey(user.Username))
            return false;
        _byUsername[user.Username] = user;
        _byId[user.Id] = user;
        return true;
    }
}
