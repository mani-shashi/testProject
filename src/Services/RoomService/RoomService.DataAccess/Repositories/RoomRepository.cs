using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomService.Business.Entities;
using RoomService.Business.Exceptions;
using RoomService.Business.Interfaces;
using RoomService.Contracts.Enums;
using RoomService.DataAccess.Context;

namespace RoomService.DataAccess.Repositories;

public sealed class RoomRepository : IRoomRepository
{
    private readonly RoomDbContext _context;

    public RoomRepository(RoomDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Room>> GetAllAsync(int? roomTypeId, RoomStatus? status, CancellationToken cancellationToken)
    {
        var query = _context.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .AsQueryable();

        if (roomTypeId.HasValue)
        {
            query = query.Where(room => room.RoomTypeId == roomTypeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(room => room.Status == status.Value);
        }

        return await query
            .OrderBy(room => room.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _context.Rooms
            .Include(room => room.RoomType)
            .FirstOrDefaultAsync(room => room.Id == id, cancellationToken);

    public async Task<bool> NumberExistsAsync(string number, int? excludeId, CancellationToken cancellationToken)
        => await _context.Rooms.AnyAsync(
            room => room.Number == number && (excludeId == null || room.Id != excludeId),
            cancellationToken);

    public async Task<IReadOnlyList<Room>> GetAvailableAsync(DateOnly checkIn, DateOnly checkOut, int? roomTypeId, CancellationToken cancellationToken)
    {
        var query = _context.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .Where(room => room.RoomType.IsActive)
            .Where(room => room.Status != RoomStatus.OutOfOrder)
            .Where(room => !room.Blocks.Any(block => block.FromDate < checkOut && block.ToDate > checkIn));

        if (roomTypeId.HasValue)
        {
            query = query.Where(room => room.RoomTypeId == roomTypeId.Value);
        }

        return await query
            .OrderBy(room => room.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task<Room> AddAsync(Room room, CancellationToken cancellationToken)
    {
        _context.Rooms.Add(room);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException($"A room with number '{room.Number}' already exists.");
        }

        return room;
    }

    public async Task UpdateAsync(Room room, CancellationToken cancellationToken)
    {
        _context.Rooms.Update(room);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException($"A room with number '{room.Number}' already exists.");
        }
    }

    public async Task DeleteAsync(Room room, CancellationToken cancellationToken)
    {
        _context.Rooms.Remove(room);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
