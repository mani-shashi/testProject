using System.ComponentModel.DataAnnotations;

namespace RoomService.Contracts.Requests;

public sealed class UpdateRoomRateRequestDto
{
    [Range(0.01, 1_000_000)]
    public decimal Amount { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }
}
