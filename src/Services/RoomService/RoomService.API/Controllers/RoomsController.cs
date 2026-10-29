using RoomService.Business.Authorization;
using RoomService.Business.Interfaces;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Enums;
using RoomService.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomService.API.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomManagementService _roomService;

    public RoomsController(IRoomManagementService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomResponseDto>>> GetAll(
        [FromQuery] int? roomTypeId,
        [FromQuery] RoomStatus? status,
        CancellationToken cancellationToken)
    {
        var rooms = await _roomService.GetAllAsync(roomTypeId, status, cancellationToken);
        return Ok(rooms);
    }

    [HttpGet("availability")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomAvailabilityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RoomAvailabilityResponseDto>>> SearchAvailability(
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] int? roomTypeId,
        CancellationToken cancellationToken)
    {
        var rooms = await _roomService.SearchAvailabilityAsync(checkIn, checkOut, roomTypeId, cancellationToken);
        return Ok(rooms);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetByIdAsync(id, cancellationToken);
        return Ok(room);
    }

    [HttpPost]
    [Authorize(Roles = Roles.OwnerOrManager)]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomResponseDto>> Create(CreateRoomRequestDto request, CancellationToken cancellationToken)
    {
        var room = await _roomService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = room.Id }, room);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.OwnerOrManager)]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomResponseDto>> Update(int id, UpdateRoomRequestDto request, CancellationToken cancellationToken)
    {
        var room = await _roomService.UpdateAsync(id, request, cancellationToken);
        return Ok(room);
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponseDto>> UpdateStatus(int id, UpdateRoomStatusRequestDto request, CancellationToken cancellationToken)
    {
        var room = await _roomService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(room);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.OwnerOrManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _roomService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
