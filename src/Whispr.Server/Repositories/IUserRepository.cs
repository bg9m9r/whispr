using Whispr.Core.Models;

namespace Whispr.Server.Repositories;

/// <summary>
/// Repository for user data access.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Loads all users from the store.
    /// </summary>
    IReadOnlyList<User> LoadAll();

    /// <summary>
    /// Gets a user by username, or null if not found.
    /// </summary>
    User? GetByUsername(string username);

    /// <summary>
    /// Gets a user by ID, or null if not found.
    /// </summary>
    User? GetById(Guid id);

    /// <summary>
    /// Inserts a new user. Returns false if username already exists.
    /// </summary>
    bool Insert(User user);
}
