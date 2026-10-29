using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomService.Business.Entities;
using RoomService.Business.Exceptions;
using RoomService.Business.Interfaces;
using RoomService.DataAccess.Context;

namespace RoomService.DataAccess.Repositories;

public sealed class RoomTypeRepository : IRoomTypeRepository
{
    private readonly RoomDbContext _context;

    public RoomTypeRepository(RoomDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RoomType>> GetAllAsync(CancellationToken cancellationToken)
        => await _context.RoomTypes
            .AsNoTracking()
            .OrderBy(roomType => roomType.Name)
            .ToListAsync(cancellationToken);

    public async Task<RoomType?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _context.RoomTypes.FirstOrDefaultAsync(roomType => roomType.Id == id, cancellationToken);

    public async Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken cancellationToken)
        => await _context.RoomTypes.AnyAsync(
            roomType => roomType.Name == name && (excludeId == null || roomType.Id != excludeId),
            cancellationToken);

    public async Task<bool> HasRoomsAsync(int roomTypeId, CancellationToken cancellationToken)
        => await _context.Rooms.AnyAsync(room => room.RoomTypeId == roomTypeId, cancellationToken);

    public async Task<RoomType> AddAsync(RoomType roomType, CancellationToken cancellationToken)
    {
        _context.RoomTypes.Add(roomType);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException($"A room type named '{roomType.Name}' already exists.");
        }

        return roomType;
    }

    public async Task UpdateAsync(RoomType roomType, CancellationToken cancellationToken)
    {
        _context.RoomTypes.Update(roomType);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException($"A room type named '{roomType.Name}' already exists.");
        }
    }

    public async Task DeleteAsync(RoomType roomType, CancellationToken cancellationToken)
    {
        _context.RoomTypes.Remove(roomType);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
