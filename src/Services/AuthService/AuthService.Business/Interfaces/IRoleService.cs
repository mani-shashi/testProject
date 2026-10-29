using AuthService.Contracts.DTOs;

namespace AuthService.Business.Interfaces;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponseDto>> GetAllAsync(CancellationToken cancellationToken);
}
