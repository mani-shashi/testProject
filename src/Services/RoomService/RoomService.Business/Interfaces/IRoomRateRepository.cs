using RoomService.Business.Entities;

namespace RoomService.Business.Interfaces;

public interface IRoomRateRepository
{
    Task<IReadOnlyList<RoomRate>> GetByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken);
    Task<RoomRate?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<RoomRate?> GetApplicableAsync(int roomTypeId, DateOnly date, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(RoomRate rate, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(RoomRate rate, CancellationToken cancellationToken);
    Task DeleteAsync(RoomRate rate, CancellationToken cancellationToken);
}
