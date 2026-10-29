using RoomService.Business.Authorization;
using RoomService.Business.Interfaces;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoomService.API.Controllers;

[ApiController]
[Route("api/room-blocks")]
[Authorize(Roles = Roles.OwnerOrManager)]
public sealed class RoomBlocksController : ControllerBase
{
    private readonly IRoomBlockService _blockService;

    public RoomBlocksController(IRoomBlockService blockService)
    {
        _blockService = blockService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomBlockResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RoomBlockResponseDto>>> GetByRoom(
        [FromQuery] int roomId,
        CancellationToken cancellationToken)
    {
        var blocks = await _blockService.GetByRoomAsync(roomId, cancellationToken);
        return Ok(blocks);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoomBlockResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomBlockResponseDto>> Create(CreateRoomBlockRequestDto request, CancellationToken cancellationToken)
    {
        var block = await _blockService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByRoom), new { roomId = block.RoomId }, block);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _blockService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
