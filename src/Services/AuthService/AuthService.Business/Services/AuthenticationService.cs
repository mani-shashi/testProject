using AuthService.Business.Exceptions;
using AuthService.Business.Interfaces;
using AuthService.Business.Mapping;
using AuthService.Business.Security;
using AuthService.Contracts.DTOs;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace AuthService.Business.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _users;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IUserRepository users, JwtTokenGenerator tokenGenerator, ILogger<AuthenticationService> logger)
    {
        _users = users;
        _tokenGenerator = tokenGenerator;
        _logger = logger;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login failed for {Email}: unknown or inactive account.", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var passwordValid = await _users.CheckPasswordAsync(user.Id, request.Password, cancellationToken);
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed for {Email}: invalid password.", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var (token, expiresAtUtc) = _tokenGenerator.Generate(user);
        _logger.LogInformation("User {UserId} logged in.", user.Id);

        return new LoginResponseDto
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            User = UserMapper.ToDto(user)
        };
    }

    public async Task<UserResponseDto> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        return UserMapper.ToDto(user);
    }
}
