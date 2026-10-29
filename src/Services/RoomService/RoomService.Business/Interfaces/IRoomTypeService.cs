using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Interfaces;

public interface IRoomTypeService
{
    Task<IReadOnlyList<RoomTypeResponseDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<RoomTypeResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<RoomTypeResponseDto> CreateAsync(CreateRoomTypeRequestDto request, CancellationToken cancellationToken);
    Task<RoomTypeResponseDto> UpdateAsync(int id, UpdateRoomTypeRequestDto request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
