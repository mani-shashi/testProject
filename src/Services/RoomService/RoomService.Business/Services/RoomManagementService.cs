using Microsoft.Extensions.Logging;
using RoomService.Business.Entities;
using RoomService.Business.Exceptions;
using RoomService.Business.Interfaces;
using RoomService.Business.Mapping;
using RoomService.Business.Rules;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Enums;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Services;

public sealed class RoomManagementService : IRoomManagementService
{
    private readonly IRoomRepository _rooms;
    private readonly IRoomTypeRepository _roomTypes;
    private readonly IRoomRateRepository _rates;
    private readonly IRoomBlockRepository _blocks;
    private readonly ILogger<RoomManagementService> _logger;

    public RoomManagementService(
        IRoomRepository rooms,
        IRoomTypeRepository roomTypes,
        IRoomRateRepository rates,
        IRoomBlockRepository blocks,
        ILogger<RoomManagementService> logger)
    {
        _rooms = rooms;
        _roomTypes = roomTypes;
        _rates = rates;
        _blocks = blocks;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoomResponseDto>> GetAllAsync(int? roomTypeId, RoomStatus? status, CancellationToken cancellationToken)
    {
        var rooms = await _rooms.GetAllAsync(roomTypeId, status, cancellationToken);
        return rooms.Select(RoomMapper.ToDto).ToList();
    }

    public async Task<RoomResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        return RoomMapper.ToDto(room);
    }

    public async Task<RoomResponseDto> CreateAsync(CreateRoomRequestDto request, CancellationToken cancellationToken)
    {
        RoomRules.EnsureStatusDefined(request.Status);

        _ = await _roomTypes.GetByIdAsync(request.RoomTypeId, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        var number = request.Number.Trim();
        if (await _rooms.NumberExistsAsync(number, null, cancellationToken))
        {
            throw new ConflictException($"A room with number '{number}' already exists.");
        }

        var room = new Room
        {
            Number = number,
            Floor = request.Floor,
            Status = request.Status,
            RoomTypeId = request.RoomTypeId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _rooms.AddAsync(room, cancellationToken);
        _logger.LogInformation("Room {RoomId} ({Number}) created.", created.Id, created.Number);

        var reloaded = await _rooms.GetByIdAsync(created.Id, cancellationToken)
            ?? throw new NotFoundException("The room could not be reloaded after creation.");

        return RoomMapper.ToDto(reloaded);
    }

    public async Task<RoomResponseDto> UpdateAsync(int id, UpdateRoomRequestDto request, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        _ = await _roomTypes.GetByIdAsync(request.RoomTypeId, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        var number = request.Number.Trim();
        if (await _rooms.NumberExistsAsync(number, id, cancellationToken))
        {
            throw new ConflictException($"A room with number '{number}' already exists.");
        }

        room.Number = number;
        room.Floor = request.Floor;
        room.RoomTypeId = request.RoomTypeId;

        await _rooms.UpdateAsync(room, cancellationToken);
        _logger.LogInformation("Room {RoomId} updated.", id);

        var reloaded = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("The room could not be reloaded after update.");

        return RoomMapper.ToDto(reloaded);
    }

    public async Task<RoomResponseDto> UpdateStatusAsync(int id, UpdateRoomStatusRequestDto request, CancellationToken cancellationToken)
    {
        RoomRules.EnsureStatusDefined(request.Status);

        var room = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        room.Status = request.Status;
        await _rooms.UpdateAsync(room, cancellationToken);
        _logger.LogInformation("Room {RoomId} status changed to {Status}.", id, request.Status);

        var reloaded = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("The room could not be reloaded after the status change.");

        return RoomMapper.ToDto(reloaded);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        await _rooms.DeleteAsync(room, cancellationToken);
        _logger.LogInformation("Room {RoomId} deleted.", id);
    }

    public async Task<IReadOnlyList<RoomAvailabilityResponseDto>> SearchAvailabilityAsync(DateOnly checkIn, DateOnly checkOut, int? roomTypeId, CancellationToken cancellationToken)
    {
        RoomRules.EnsureStayDates(checkIn, checkOut);

        var rooms = await _rooms.GetAvailableAsync(checkIn, checkOut, roomTypeId, cancellationToken);
        var nightlyRates = await ResolveNightlyRatesAsync(rooms, checkIn, cancellationToken);

        return rooms
            .Select(room => RoomMapper.ToAvailabilityDto(room, nightlyRates[room.RoomTypeId]))
            .ToList();
    }

    public async Task<InternalRoomResponseDto> GetInternalRoomAsync(int id, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken)
    {
        RoomRules.EnsureStayDates(checkIn, checkOut);

        var room = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        var isBlocked = await _blocks.IsRoomBlockedAsync(id, checkIn, checkOut, cancellationToken);
        var rate = await _rates.GetApplicableAsync(room.RoomTypeId, checkIn, cancellationToken);
        var nightlyRate = rate?.Amount ?? room.RoomType?.BasePrice ?? 0m;

        return RoomMapper.ToInternalDto(room, nightlyRate, isBlocked);
    }

    private async Task<Dictionary<int, decimal>> ResolveNightlyRatesAsync(IEnumerable<Room> rooms, DateOnly date, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, decimal>();

        foreach (var group in rooms.GroupBy(room => room.RoomTypeId))
        {
            var rate = await _rates.GetApplicableAsync(group.Key, date, cancellationToken);
            result[group.Key] = rate?.Amount ?? group.First().RoomType?.BasePrice ?? 0m;
        }

        return result;
    }
}
