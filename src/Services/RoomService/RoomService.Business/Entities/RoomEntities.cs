using RoomService.Contracts.Enums;

namespace RoomService.Business.Entities;

public sealed class RoomType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int MaxOccupancy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<RoomRate> Rates { get; set; } = new List<RoomRate>();
}

public sealed class Room
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public int RoomTypeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public RoomType RoomType { get; set; } = null!;
    public ICollection<RoomBlock> Blocks { get; set; } = new List<RoomBlock>();
}

public sealed class RoomRate
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public RoomType RoomType { get; set; } = null!;
}

public sealed class RoomBlock
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public Room Room { get; set; } = null!;
}
