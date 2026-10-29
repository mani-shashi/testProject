using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Interfaces;

public interface IRoomRateService
{
    Task<IReadOnlyList<RoomRateResponseDto>> GetByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken);
    Task<RoomRateResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<RoomRateResponseDto> CreateAsync(CreateRoomRateRequestDto request, CancellationToken cancellationToken);
    Task<RoomRateResponseDto> UpdateAsync(int id, UpdateRoomRateRequestDto request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
