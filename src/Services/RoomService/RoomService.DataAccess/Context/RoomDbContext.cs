using Microsoft.EntityFrameworkCore;
using RoomService.Business.Entities;

namespace RoomService.DataAccess.Context;

public sealed class RoomDbContext : DbContext
{
    public RoomDbContext(DbContextOptions<RoomDbContext> options) : base(options)
    {
    }

    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomRate> RoomRates => Set<RoomRate>();
    public DbSet<RoomBlock> RoomBlocks => Set<RoomBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomDbContext).Assembly);
    }
}
