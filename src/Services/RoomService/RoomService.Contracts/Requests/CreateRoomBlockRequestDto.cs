using System.ComponentModel.DataAnnotations;

namespace RoomService.Contracts.Requests;

public sealed class CreateRoomBlockRequestDto
{
    [Range(1, int.MaxValue)]
    public int RoomId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;
}
