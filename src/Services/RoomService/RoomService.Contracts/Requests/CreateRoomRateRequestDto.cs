using System.ComponentModel.DataAnnotations;

namespace RoomService.Contracts.Requests;

public sealed class CreateRoomRateRequestDto
{
    [Range(1, int.MaxValue)]
    public int RoomTypeId { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal Amount { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }
}
