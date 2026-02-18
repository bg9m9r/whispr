namespace Whispr.Server.Repositories;

/// <summary>
/// In-memory channel repository for development/testing when no database is configured.
/// </summary>
public sealed class InMemoryChannelRepository : IChannelRepository
{
    public IReadOnlyList<(Guid Id, string Name, byte[] KeyMaterial, bool IsDefault)> LoadAll() => [];

    public bool Insert(Guid id, string name, byte[] keyMaterial) => true;
}
