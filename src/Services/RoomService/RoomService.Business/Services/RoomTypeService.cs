using Microsoft.Extensions.Logging;
using RoomService.Business.Entities;
using RoomService.Business.Exceptions;
using RoomService.Business.Interfaces;
using RoomService.Business.Mapping;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;

namespace RoomService.Business.Services;

public sealed class RoomTypeService : IRoomTypeService
{
    private readonly IRoomTypeRepository _roomTypes;
    private readonly ILogger<RoomTypeService> _logger;

    public RoomTypeService(IRoomTypeRepository roomTypes, ILogger<RoomTypeService> logger)
    {
        _roomTypes = roomTypes;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoomTypeResponseDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var roomTypes = await _roomTypes.GetAllAsync(cancellationToken);
        return roomTypes.Select(RoomMapper.ToDto).ToList();
    }

    public async Task<RoomTypeResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        return RoomMapper.ToDto(roomType);
    }

    public async Task<RoomTypeResponseDto> CreateAsync(CreateRoomTypeRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await _roomTypes.NameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException($"A room type named '{name}' already exists.");
        }

        var roomType = new RoomType
        {
            Name = name,
            Description = request.Description?.Trim(),
            BasePrice = request.BasePrice,
            MaxOccupancy = request.MaxOccupancy,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _roomTypes.AddAsync(roomType, cancellationToken);
        _logger.LogInformation("Room type {RoomTypeId} created.", created.Id);

        return RoomMapper.ToDto(created);
    }

    public async Task<RoomTypeResponseDto> UpdateAsync(int id, UpdateRoomTypeRequestDto request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        var name = request.Name.Trim();
        if (await _roomTypes.NameExistsAsync(name, id, cancellationToken))
        {
            throw new ConflictException($"A room type named '{name}' already exists.");
        }

        roomType.Name = name;
        roomType.Description = request.Description?.Trim();
        roomType.BasePrice = request.BasePrice;
        roomType.MaxOccupancy = request.MaxOccupancy;
        roomType.IsActive = request.IsActive;

        await _roomTypes.UpdateAsync(roomType, cancellationToken);
        _logger.LogInformation("Room type {RoomTypeId} updated.", id);

        return RoomMapper.ToDto(roomType);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Room type not found.");

        if (await _roomTypes.HasRoomsAsync(id, cancellationToken))
        {
            throw new ConflictException("The room type still has rooms and cannot be deleted.");
        }

        await _roomTypes.DeleteAsync(roomType, cancellationToken);
        _logger.LogInformation("Room type {RoomTypeId} deleted.", id);
    }
}
