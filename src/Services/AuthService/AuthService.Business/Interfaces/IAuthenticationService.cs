using AuthService.Contracts.DTOs;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;

namespace AuthService.Business.Interfaces;

public interface IAuthenticationService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<UserResponseDto> GetCurrentUserAsync(string userId, CancellationToken cancellationToken);
}
