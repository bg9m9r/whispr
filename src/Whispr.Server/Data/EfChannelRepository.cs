using Microsoft.EntityFrameworkCore;
using Whispr.Core.Models;
using Whispr.Server.Repositories;

namespace Whispr.Server.Data;

/// <summary>
/// Entity Framework-backed channel repository.
/// </summary>
public sealed class EfChannelRepository(IDbContextFactory<WhisprDbContext> contextFactory) : IChannelRepository
{
    public IReadOnlyList<(Guid Id, string Name, byte[] KeyMaterial, bool IsDefault, ChannelType Type)> LoadAll()
    {
        using var ctx = contextFactory.CreateDbContext();
        return ctx.Channels.AsNoTracking()
            .ToList()
            .Select(c => (Guid.Parse(c.Id), c.Name, c.KeyMaterial, c.IsDefault, (ChannelType)c.ChannelType))
            .ToList();
    }

    public bool Insert(Guid id, string name, byte[] keyMaterial, ChannelType type)
    {
        using var ctx = contextFactory.CreateDbContext();
        ctx.Channels.Add(new ChannelEntity
        {
            Id = id.ToString(),
            Name = name,
            KeyMaterial = keyMaterial,
            IsDefault = false,
            ChannelType = (int)type
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
