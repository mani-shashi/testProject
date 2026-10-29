using Microsoft.Extensions.Logging;
using RoomService.Business.Entities;
using RoomService.Business.Exceptions;
using RoomService.Business.Interfaces;
using RoomService.Business.Mapping;
using RoomService.Business.Rules;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Services;

public sealed class RoomBlockService : IRoomBlockService
{
    private readonly IRoomBlockRepository _blocks;
    private readonly IRoomRepository _rooms;
    private readonly ILogger<RoomBlockService> _logger;

    public RoomBlockService(IRoomBlockRepository blocks, IRoomRepository rooms, ILogger<RoomBlockService> logger)
    {
        _blocks = blocks;
        _rooms = rooms;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoomBlockResponseDto>> GetByRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        _ = await _rooms.GetByIdAsync(roomId, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        var blocks = await _blocks.GetByRoomAsync(roomId, cancellationToken);
        return blocks.Select(RoomMapper.ToDto).ToList();
    }

    public async Task<RoomBlockResponseDto> CreateAsync(CreateRoomBlockRequestDto request, CancellationToken cancellationToken)
    {
        RoomRules.EnsureInclusiveRange(request.FromDate, request.ToDate, "Block period");

        _ = await _rooms.GetByIdAsync(request.RoomId, cancellationToken)
            ?? throw new NotFoundException("Room not found.");

        var block = new RoomBlock
        {
            RoomId = request.RoomId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Reason = request.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _blocks.AddAsync(block, cancellationToken);
        _logger.LogInformation("Room {RoomId} blocked from {From} to {To}.", request.RoomId, request.FromDate, request.ToDate);

        var reloaded = await _blocks.GetByIdAsync(created.Id, cancellationToken)
            ?? throw new NotFoundException("The room block could not be reloaded after creation.");

        return RoomMapper.ToDto(reloaded);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var block = await _blocks.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room block not found.");

        await _blocks.DeleteAsync(block, cancellationToken);
        _logger.LogInformation("Room block {RoomBlockId} deleted.", id);
    }
}
