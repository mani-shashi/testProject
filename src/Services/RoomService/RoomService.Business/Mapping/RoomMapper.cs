using RoomService.Business.Entities;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Enums;

namespace RoomService.Business.Mapping;

public static class RoomMapper
{
    public static RoomTypeResponseDto ToDto(RoomType roomType) => new()
    {
        Id = roomType.Id,
        Name = roomType.Name,
        Description = roomType.Description,
        BasePrice = roomType.BasePrice,
        MaxOccupancy = roomType.MaxOccupancy,
        IsActive = roomType.IsActive,
        CreatedAtUtc = roomType.CreatedAtUtc
    };

    public static RoomResponseDto ToDto(Room room) => new()
    {
        Id = room.Id,
        Number = room.Number,
        Floor = room.Floor,
        Status = room.Status,
        RoomTypeId = room.RoomTypeId,
        RoomTypeName = room.RoomType?.Name ?? string.Empty,
        BasePrice = room.RoomType?.BasePrice ?? 0m,
        CreatedAtUtc = room.CreatedAtUtc
    };

    public static RoomRateResponseDto ToDto(RoomRate rate) => new()
    {
        Id = rate.Id,
        RoomTypeId = rate.RoomTypeId,
        RoomTypeName = rate.RoomType?.Name ?? string.Empty,
        Amount = rate.Amount,
        ValidFrom = rate.ValidFrom,
        ValidTo = rate.ValidTo,
        CreatedAtUtc = rate.CreatedAtUtc
    };

    public static RoomBlockResponseDto ToDto(RoomBlock block) => new()
    {
        Id = block.Id,
        RoomId = block.RoomId,
        RoomNumber = block.Room?.Number ?? string.Empty,
        FromDate = block.FromDate,
        ToDate = block.ToDate,
        Reason = block.Reason,
        CreatedAtUtc = block.CreatedAtUtc
    };

    public static RoomAvailabilityResponseDto ToAvailabilityDto(Room room, decimal nightlyRate) => new()
    {
        Id = room.Id,
        Number = room.Number,
        Floor = room.Floor,
        Status = room.Status,
        RoomTypeId = room.RoomTypeId,
        RoomTypeName = room.RoomType?.Name ?? string.Empty,
        NightlyRate = nightlyRate
    };

    public static InternalRoomResponseDto ToInternalDto(Room room, decimal nightlyRate, bool isBlocked) => new()
    {
        Id = room.Id,
        Number = room.Number,
        Floor = room.Floor,
        Status = room.Status,
        RoomTypeId = room.RoomTypeId,
        RoomTypeName = room.RoomType?.Name ?? string.Empty,
        BasePrice = room.RoomType?.BasePrice ?? 0m,
        NightlyRate = nightlyRate,
        IsBlocked = isBlocked,
        IsAvailable = room.Status != RoomStatus.OutOfOrder && !isBlocked
    };
}
