using RoomService.Business.Authorization;
using RoomService.Business.Interfaces;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomService.API.Controllers;

[ApiController]
[Route("api/room-types")]
[Authorize]
public sealed class RoomTypesController : ControllerBase
{
    private readonly IRoomTypeService _roomTypeService;

    public RoomTypesController(IRoomTypeService roomTypeService)
    {
        _roomTypeService = roomTypeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomTypeResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomTypeResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var roomTypes = await _roomTypeService.GetAllAsync(cancellationToken);
        return Ok(roomTypes);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoomTypeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomTypeResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.GetByIdAsync(id, cancellationToken);
        return Ok(roomType);
    }

    [HttpPost]
    [Authorize(Roles = Roles.OwnerOrManager)]
    [ProducesResponseType(typeof(RoomTypeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomTypeResponseDto>> Create(CreateRoomTypeRequestDto request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = roomType.Id }, roomType);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.OwnerOrManager)]
    [ProducesResponseType(typeof(RoomTypeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomTypeResponseDto>> Update(int id, UpdateRoomTypeRequestDto request, CancellationToken cancellationToken)
    {
        var roomType = await _roomTypeService.UpdateAsync(id, request, cancellationToken);
        return Ok(roomType);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.OwnerOrManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _roomTypeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
