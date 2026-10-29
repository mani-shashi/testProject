using Microsoft.EntityFrameworkCore;
using RoomService.Business.Entities;
using RoomService.Business.Interfaces;
using RoomService.DataAccess.Context;

namespace RoomService.DataAccess.Repositories;

public sealed class RoomBlockRepository : IRoomBlockRepository
{
    private readonly RoomDbContext _context;

    public RoomBlockRepository(RoomDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RoomBlock>> GetByRoomAsync(int roomId, CancellationToken cancellationToken)
        => await _context.RoomBlocks
            .AsNoTracking()
            .Include(block => block.Room)
            .Where(block => block.RoomId == roomId)
            .OrderBy(block => block.FromDate)
            .ToListAsync(cancellationToken);

    public async Task<RoomBlock?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _context.RoomBlocks
            .Include(block => block.Room)
            .FirstOrDefaultAsync(block => block.Id == id, cancellationToken);

    public async Task<bool> IsRoomBlockedAsync(int roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken)
        => await _context.RoomBlocks.AnyAsync(
            block => block.RoomId == roomId && block.FromDate < checkOut && block.ToDate > checkIn,
            cancellationToken);

    public async Task<RoomBlock> AddAsync(RoomBlock block, CancellationToken cancellationToken)
    {
        _context.RoomBlocks.Add(block);
        await _context.SaveChangesAsync(cancellationToken);
        return block;
    }

    public async Task DeleteAsync(RoomBlock block, CancellationToken cancellationToken)
    {
        _context.RoomBlocks.Remove(block);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
