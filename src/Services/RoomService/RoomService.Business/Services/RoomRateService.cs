using Microsoft.Extensions.Logging;
using RoomService.Business.Entities;
using RoomService.Business.Exceptions;
using RoomService.Business.Interfaces;
using RoomService.Business.Mapping;
using RoomService.Business.Rules;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Services;

public sealed class RoomRateService : IRoomRateService
{
    private readonly IRoomRateRepository _rates;
    private readonly IRoomTypeRepository _roomTypes;
    private readonly ILogger<RoomRateService> _logger;

    public RoomRateService(IRoomRateRepository rates, IRoomTypeRepository roomTypes, ILogger<RoomRateService> logger)
    {
        _rates = rates;
        _roomTypes = roomTypes;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoomRateResponseDto>> GetByRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken)
    {
        _ = await _roomTypes.GetByIdAsync(roomTypeId, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        var rates = await _rates.GetByRoomTypeAsync(roomTypeId, cancellationToken);
        return rates.Select(RoomMapper.ToDto).ToList();
    }

    public async Task<RoomRateResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var rate = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room rate not found.");

        return RoomMapper.ToDto(rate);
    }

    public async Task<RoomRateResponseDto> CreateAsync(CreateRoomRateRequestDto request, CancellationToken cancellationToken)
    {
        RoomRules.EnsureInclusiveRange(request.ValidFrom, request.ValidTo, "Rate validity");
        RoomRules.EnsureAmount(request.Amount);

        _ = await _roomTypes.GetByIdAsync(request.RoomTypeId, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        var rate = new RoomRate
        {
            RoomTypeId = request.RoomTypeId,
            Amount = request.Amount,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!await _rates.TryAddAsync(rate, cancellationToken))
        {
            throw new ConflictException("A rate for this room type already covers part of the requested period.");
        }

        _logger.LogInformation("Room rate {RoomRateId} created for room type {RoomTypeId}.", rate.Id, rate.RoomTypeId);

        var reloaded = await _rates.GetByIdAsync(rate.Id, cancellationToken)
            ?? throw new NotFoundException("The room rate could not be reloaded after creation.");

        return RoomMapper.ToDto(reloaded);
    }

    public async Task<RoomRateResponseDto> UpdateAsync(int id, UpdateRoomRateRequestDto request, CancellationToken cancellationToken)
    {
        var rate = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room rate not found.");

        RoomRules.EnsureInclusiveRange(request.ValidFrom, request.ValidTo, "Rate validity");
        RoomRules.EnsureAmount(request.Amount);

        rate.Amount = request.Amount;
        rate.ValidFrom = request.ValidFrom;
        rate.ValidTo = request.ValidTo;

        if (!await _rates.TryUpdateAsync(rate, cancellationToken))
        {
            throw new ConflictException("A rate for this room type already covers part of the requested period.");
        }

        _logger.LogInformation("Room rate {RoomRateId} updated.", id);

        var reloaded = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("The room rate could not be reloaded after update.");

        return RoomMapper.ToDto(reloaded);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var rate = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room rate not found.");

        await _rates.DeleteAsync(rate, cancellationToken);
        _logger.LogInformation("Room rate {RoomRateId} deleted.", id);
    }
}
