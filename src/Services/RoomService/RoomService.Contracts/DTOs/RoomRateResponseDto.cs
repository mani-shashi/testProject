namespace RoomService.Contracts.DTOs;

public sealed class RoomRateResponseDto
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
