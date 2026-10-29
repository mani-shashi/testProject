using RoomService.Business.Interfaces;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;
using Microsoft.AspNetCore.Mvc;

namespace RoomService.API.Controllers;

[ApiController]
[Route("internal/rooms")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalRoomsController : ControllerBase
{
    private readonly IRoomManagementService _roomService;

    public InternalRoomsController(IRoomManagementService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet("availability")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomAvailabilityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RoomAvailabilityResponseDto>>> Availability(
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] int? roomTypeId,
        CancellationToken cancellationToken)
    {
        var rooms = await _roomService.SearchAvailabilityAsync(checkIn, checkOut, roomTypeId, cancellationToken);
        return Ok(rooms);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InternalRoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InternalRoomResponseDto>> GetById(
        int id,
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        var room = await _roomService.GetInternalRoomAsync(id, checkIn, checkOut, cancellationToken);
        return Ok(room);
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponseDto>> UpdateStatus(
        int id,
        UpdateRoomStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var room = await _roomService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(room);
    }
}
