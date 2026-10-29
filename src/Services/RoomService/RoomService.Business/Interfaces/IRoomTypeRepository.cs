using RoomService.Business.Entities;

namespace RoomService.Business.Interfaces;

public interface IRoomTypeRepository
{
    Task<IReadOnlyList<RoomType>> GetAllAsync(CancellationToken cancellationToken);
    Task<RoomType?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken cancellationToken);
    Task<bool> HasRoomsAsync(int roomTypeId, CancellationToken cancellationToken);
    Task<RoomType> AddAsync(RoomType roomType, CancellationToken cancellationToken);
    Task UpdateAsync(RoomType roomType, CancellationToken cancellationToken);
    Task DeleteAsync(RoomType roomType, CancellationToken cancellationToken);
}
