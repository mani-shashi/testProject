using RoomService.Business.Authorization;
using RoomService.Business.Interfaces;
using RoomService.Contracts.DTOs;
using RoomService.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RoomService.API.Controllers;

[ApiController]
[Route("api/rates")]
[Authorize(Roles = Roles.OwnerOrManager)]
public sealed class RatesController : ControllerBase
{
    private readonly IRoomRateService _rateService;

    public RatesController(IRoomRateService rateService)
    {
        _rateService = rateService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomRateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RoomRateResponseDto>>> GetByRoomType(
        [FromQuery, BindRequired] int roomTypeId,
        CancellationToken cancellationToken)
    {
        var rates = await _rateService.GetByRoomTypeAsync(roomTypeId, cancellationToken);
        return Ok(rates);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoomRateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomRateResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var rate = await _rateService.GetByIdAsync(id, cancellationToken);
        return Ok(rate);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoomRateResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomRateResponseDto>> Create(CreateRoomRateRequestDto request, CancellationToken cancellationToken)
    {
        var rate = await _rateService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = rate.Id }, rate);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RoomRateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomRateResponseDto>> Update(int id, UpdateRoomRateRequestDto request, CancellationToken cancellationToken)
    {
        var rate = await _rateService.UpdateAsync(id, request, cancellationToken);
        return Ok(rate);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _rateService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
