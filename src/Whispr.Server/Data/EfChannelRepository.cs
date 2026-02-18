using Microsoft.EntityFrameworkCore;
using Whispr.Server.Repositories;

namespace Whispr.Server.Data;

/// <summary>
/// Entity Framework-backed channel repository.
/// </summary>
public sealed class EfChannelRepository(IDbContextFactory<WhisprDbContext> contextFactory) : IChannelRepository
{
    public IReadOnlyList<(Guid Id, string Name, byte[] KeyMaterial, bool IsDefault)> LoadAll()
    {
        using var ctx = contextFactory.CreateDbContext();
        return ctx.Channels.AsNoTracking()
            .ToList()
            .Select(c => (Guid.Parse(c.Id), c.Name, c.KeyMaterial, c.IsDefault))
            .ToList();
    }

    public bool Insert(Guid id, string name, byte[] keyMaterial)
    {
        using var ctx = contextFactory.CreateDbContext();
        ctx.Channels.Add(new ChannelEntity
        {
            Id = id.ToString(),
            Name = name,
            KeyMaterial = keyMaterial,
            IsDefault = false
        });
        try
        {
            ctx.SaveChanges();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
