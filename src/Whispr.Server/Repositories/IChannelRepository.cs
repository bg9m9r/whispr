using Whispr.Core.Models;

namespace Whispr.Server.Repositories;

/// <summary>
/// Repository for channel data access.
/// </summary>
public interface IChannelRepository
{
    /// <summary>
    /// Loads all channels from the store. KeyMaterial is empty for text channels.
    /// </summary>
    IReadOnlyList<(Guid Id, string Name, byte[] KeyMaterial, bool IsDefault, ChannelType Type)> LoadAll();

    /// <summary>
    /// Inserts a new channel. keyMaterial is empty for text channels. Returns true on success.
    /// </summary>
    bool Insert(Guid id, string name, byte[] keyMaterial, ChannelType type);
}
