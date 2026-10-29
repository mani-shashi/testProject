using System.ComponentModel.DataAnnotations;

namespace RoomService.Contracts.Requests;

public sealed class UpdateRoomRequestDto
{
    [Required]
    [MaxLength(10)]
    public string Number { get; set; } = string.Empty;

    [Range(-5, 200)]
    public int Floor { get; set; }

    [Range(1, int.MaxValue)]
    public int RoomTypeId { get; set; }
}
