using AuthService.Business.Models;

namespace AuthService.Business.Interfaces;

public interface IUserRepository
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserAccount?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserTokenStatus?> GetTokenStatusAsync(string id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<UserOperationResult> CreateAsync(string email, string fullName, string password, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task<UserOperationResult> UpdateAsync(string id, string fullName, bool isActive, CancellationToken cancellationToken);
    Task<UserOperationResult> DeleteAsync(string id, CancellationToken cancellationToken);
    Task<UserOperationResult> ReplaceRolesAsync(string id, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task<bool> CheckPasswordAsync(string id, string password, CancellationToken cancellationToken);
}
