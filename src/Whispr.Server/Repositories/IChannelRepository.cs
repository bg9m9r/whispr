namespace Whispr.Server.Repositories;

/// <summary>
/// Repository for channel data access.
/// </summary>
public interface IChannelRepository
{
    /// <summary>
    /// Loads all channels from the store.
    /// </summary>
    IReadOnlyList<(Guid Id, string Name, byte[] KeyMaterial, bool IsDefault)> LoadAll();

    /// <summary>
    /// Inserts a new channel. Returns true on success.
    /// </summary>
    bool Insert(Guid id, string name, byte[] keyMaterial);
}
