using AuthService.Contracts.DTOs;
using AuthService.Contracts.Requests;

namespace AuthService.Business.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserResponseDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<UserResponseDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken);
    Task<UserResponseDto> UpdateAsync(string id, UpdateUserRequestDto request, string currentUserId, CancellationToken cancellationToken);
    Task<UserResponseDto> AssignRolesAsync(string id, AssignRolesRequestDto request, string currentUserId, CancellationToken cancellationToken);
    Task DeleteAsync(string id, string currentUserId, CancellationToken cancellationToken);
}
