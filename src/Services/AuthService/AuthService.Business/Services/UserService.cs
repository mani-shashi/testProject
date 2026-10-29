using AuthService.Business.Exceptions;
using AuthService.Business.Interfaces;
using AuthService.Business.Mapping;
using AuthService.Business.Models;
using AuthService.Business.Rules;
using AuthService.Contracts.DTOs;
using AuthService.Contracts.Requests;
using Microsoft.Extensions.Logging;

namespace AuthService.Business.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository users, ILogger<UserService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserResponseDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await _users.GetAllAsync(cancellationToken);
        return users.Select(UserMapper.ToDto).ToList();
    }

    public async Task<UserResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        return UserMapper.ToDto(user);
    }

    public async Task<UserResponseDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();

        if (fullName.Length == 0)
        {
            throw new ValidationException("Full name is required.");
        }

        var roles = UserRules.NormalizeRoles(request.Roles);

        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException($"A user with email '{email}' already exists.");
        }

        var result = await _users.CreateAsync(email, fullName, request.Password, roles, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorCodes.Contains("DuplicateEmail") || result.ErrorCodes.Contains("DuplicateUserName"))
            {
                throw new ConflictException($"A user with email '{email}' already exists.");
            }

            throw new ValidationException(string.Join(" ", result.Errors));
        }

        var created = await _users.GetByEmailAsync(email, cancellationToken)
            ?? throw new NotFoundException("The user could not be reloaded after creation.");

        _logger.LogInformation("User {UserId} created with roles {Roles}.", created.Id, string.Join(", ", roles));
        return UserMapper.ToDto(created);
    }

    public async Task<UserResponseDto> UpdateAsync(string id, UpdateUserRequestDto request, string currentUserId, CancellationToken cancellationToken)
    {
        var existing = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var fullName = request.FullName.Trim();
        if (fullName.Length == 0)
        {
            throw new ValidationException("Full name is required.");
        }

        if (!request.IsActive)
        {
            UserRules.EnsureNotSelf(id, currentUserId, "deactivate");
        }

        var result = await _users.UpdateAsync(id, fullName, request.IsActive, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(string.Join(" ", result.Errors));
        }

        _logger.LogInformation("User {UserId} updated (active: {IsActive}).", id, request.IsActive);

        var updated = await _users.GetByIdAsync(existing.Id, cancellationToken)
            ?? throw new NotFoundException("The user could not be reloaded after update.");

        return UserMapper.ToDto(updated);
    }

    public async Task<UserResponseDto> AssignRolesAsync(string id, AssignRolesRequestDto request, string currentUserId, CancellationToken cancellationToken)
    {
        _ = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var roles = UserRules.NormalizeRoles(request.Roles);
        UserRules.EnsureOwnerRoleRetained(id, currentUserId, roles);

        var result = await _users.ReplaceRolesAsync(id, roles, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(string.Join(" ", result.Errors));
        }

        _logger.LogInformation("Roles for user {UserId} replaced with {Roles}.", id, string.Join(", ", roles));

        var updated = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("The user could not be reloaded after role assignment.");

        return UserMapper.ToDto(updated);
    }

    public async Task DeleteAsync(string id, string currentUserId, CancellationToken cancellationToken)
    {
        UserRules.EnsureNotSelf(id, currentUserId, "delete");

        _ = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var result = await _users.DeleteAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(string.Join(" ", result.Errors));
        }

        _logger.LogInformation("User {UserId} deleted.", id);
    }
}
