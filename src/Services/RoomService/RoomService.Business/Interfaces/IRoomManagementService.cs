using RoomService.Contracts.DTOs;
using RoomService.Contracts.Enums;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Interfaces;

public interface IRoomManagementService
{
    Task<IReadOnlyList<RoomResponseDto>> GetAllAsync(int? roomTypeId, RoomStatus? status, CancellationToken cancellationToken);
    Task<RoomResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<RoomResponseDto> CreateAsync(CreateRoomRequestDto request, CancellationToken cancellationToken);
    Task<RoomResponseDto> UpdateAsync(int id, UpdateRoomRequestDto request, CancellationToken cancellationToken);
    Task<RoomResponseDto> UpdateStatusAsync(int id, UpdateRoomStatusRequestDto request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoomAvailabilityResponseDto>> SearchAvailabilityAsync(DateOnly checkIn, DateOnly checkOut, int? roomTypeId, CancellationToken cancellationToken);
    Task<InternalRoomResponseDto> GetInternalRoomAsync(int id, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken);
}
