using RoomService.Business.Entities;

namespace RoomService.Business.Interfaces;

public interface IRoomBlockRepository
{
    Task<IReadOnlyList<RoomBlock>> GetByRoomAsync(int roomId, CancellationToken cancellationToken);
    Task<RoomBlock?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> IsRoomBlockedAsync(int roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken);
    Task<RoomBlock> AddAsync(RoomBlock block, CancellationToken cancellationToken);
    Task DeleteAsync(RoomBlock block, CancellationToken cancellationToken);
}
