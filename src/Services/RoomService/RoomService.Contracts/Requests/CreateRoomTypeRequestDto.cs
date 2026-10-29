using System.ComponentModel.DataAnnotations;

namespace RoomService.Contracts.Requests;

public sealed class CreateRoomTypeRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal BasePrice { get; set; }

    [Range(1, 20)]
    public int MaxOccupancy { get; set; } = 2;
}
