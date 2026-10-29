using AuthService.Business.Interfaces;
using AuthService.Contracts.DTOs;

namespace AuthService.Business.Services;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roles;

    public RoleService(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<IReadOnlyList<RoleResponseDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var roles = await _roles.GetAllAsync(cancellationToken);
        return roles.Select(name => new RoleResponseDto { Name = name }).ToList();
    }
}
