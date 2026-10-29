using AuthService.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Route("internal/users")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IUserRepository _users;

    public InternalUsersController(IUserRepository users)
    {
        _users = users;
    }

    [HttpGet("{id}/token-status")]
    public async Task<ActionResult<InternalTokenStatusResponse>> GetTokenStatus(
        string id,
        CancellationToken cancellationToken)
    {
        var status = await _users.GetTokenStatusAsync(id, cancellationToken);
        return status is null
            ? NotFound()
            : Ok(new InternalTokenStatusResponse(status.IsActive, status.SecurityStamp));
    }
}

public sealed record InternalTokenStatusResponse(bool IsActive, string SecurityStamp);
