using Microsoft.EntityFrameworkCore;
using Whispr.Server.Data;
using Whispr.Server.Repositories;

namespace Whispr.Server.Server;

/// <summary>
/// Entity Framework-backed channel repository.
/// </summary>
public sealed class EfChannelRepository : IChannelRepository
{
    private readonly IDbContextFactory<WhisprDbContext> _contextFactory;

    public EfChannelRepository(IDbContextFactory<WhisprDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<(Guid Id, string Name, byte[] KeyMaterial, bool IsDefault)> LoadAll()
    {
        using var ctx = _contextFactory.CreateDbContext();
        return ctx.Channels.AsNoTracking()
            .ToList()
            .Select(c => (Guid.Parse(c.Id), c.Name, c.KeyMaterial, c.IsDefault))
            .ToList();
    }

    public bool Insert(Guid id, string name, byte[] keyMaterial)
    {
        using var ctx = _contextFactory.CreateDbContext();
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
