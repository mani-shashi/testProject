using AuthService.Business.Models;

namespace AuthService.Business.Interfaces;

public interface IRoleRepository
{
    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserOperationResult> CreateAsync(string name, CancellationToken cancellationToken);
}
