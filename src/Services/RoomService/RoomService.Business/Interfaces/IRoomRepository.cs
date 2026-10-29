using RoomService.Business.Entities;
using RoomService.Contracts.Enums;

namespace RoomService.Business.Interfaces;

public interface IRoomRepository
{
    Task<IReadOnlyList<Room>> GetAllAsync(int? roomTypeId, RoomStatus? status, CancellationToken cancellationToken);
    Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> NumberExistsAsync(string number, int? excludeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Room>> GetAvailableAsync(DateOnly checkIn, DateOnly checkOut, int? roomTypeId, CancellationToken cancellationToken);
    Task<Room> AddAsync(Room room, CancellationToken cancellationToken);
    Task UpdateAsync(Room room, CancellationToken cancellationToken);
    Task DeleteAsync(Room room, CancellationToken cancellationToken);
}
