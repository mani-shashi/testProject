using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Interfaces;

public interface IRoomBlockService
{
    Task<IReadOnlyList<RoomBlockResponseDto>> GetByRoomAsync(int roomId, CancellationToken cancellationToken);
    Task<RoomBlockResponseDto> CreateAsync(CreateRoomBlockRequestDto request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
