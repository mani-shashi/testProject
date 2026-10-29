using RoomService.Contracts.Enums;

namespace RoomService.Contracts.Requests;

public sealed class UpdateRoomStatusRequestDto
{
    public RoomStatus Status { get; set; }
}
