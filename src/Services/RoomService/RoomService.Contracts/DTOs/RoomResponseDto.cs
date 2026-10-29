using RoomService.Contracts.Enums;

namespace RoomService.Contracts.DTOs;

public sealed class RoomResponseDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public RoomStatus Status { get; set; }
    public int RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
