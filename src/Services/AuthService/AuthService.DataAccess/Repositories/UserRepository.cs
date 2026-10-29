using AuthService.Business.Interfaces;
using AuthService.Business.Models;
using AuthService.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AuthService.DataAccess.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly UserManager<AppUser> _userManager;

    public UserRepository(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var accounts = new List<UserAccount>(users.Count);
        foreach (var user in users)
        {
            accounts.Add(await MapAsync(user));
        }

        return accounts;
    }

    public async Task<UserAccount?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user is null ? null : await MapAsync(user);
    }

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : await MapAsync(user);
    }

    public async Task<UserTokenStatus?> GetTokenStatusAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user is null
            ? null
            : new UserTokenStatus(user.Id, user.IsActive, user.SecurityStamp ?? string.Empty);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        => await _userManager.FindByEmailAsync(email) is not null;

    public async Task<UserOperationResult> CreateAsync(string email, string fullName, string password, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult createResult;
        try
        {
            createResult = await _userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return UserOperationResult.Failure(
                new[] { $"A user with email '{email}' already exists." },
                new[] { "DuplicateEmail" });
        }

        if (!createResult.Succeeded)
        {
            return UserOperationResult.Failure(
                createResult.Errors.Select(error => error.Description),
                createResult.Errors.Select(error => error.Code));
        }

        if (roles.Count > 0)
        {
            var roleResult = await _userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                return UserOperationResult.Failure(
                    roleResult.Errors.Select(error => error.Description),
                    roleResult.Errors.Select(error => error.Code));
            }
        }

        return UserOperationResult.Success();
    }

    public async Task<UserOperationResult> UpdateAsync(string id, string fullName, bool isActive, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return UserOperationResult.Failure(new[] { "User not found." });
        }

        var wasActive = user.IsActive;
        user.FullName = fullName;
        user.IsActive = isActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return UserOperationResult.Failure(
                result.Errors.Select(error => error.Description),
                result.Errors.Select(error => error.Code));
        }

        if (wasActive != isActive)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return UserOperationResult.Failure(
                    stampResult.Errors.Select(error => error.Description),
                    stampResult.Errors.Select(error => error.Code));
            }
        }

        return UserOperationResult.Success();
    }

    public async Task<UserOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return UserOperationResult.Failure(new[] { "User not found." });
        }

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? UserOperationResult.Success()
            : UserOperationResult.Failure(result.Errors.Select(error => error.Description));
    }

    public async Task<UserOperationResult> ReplaceRolesAsync(string id, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return UserOperationResult.Failure(new[] { "User not found." });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return UserOperationResult.Failure(removeResult.Errors.Select(error => error.Description));
            }
        }

        if (roles.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, roles);
            if (!addResult.Succeeded)
            {
                return UserOperationResult.Failure(
                    addResult.Errors.Select(error => error.Description),
                    addResult.Errors.Select(error => error.Code));
            }
        }

        var rolesChanged = !currentRoles
            .OrderBy(role => role, StringComparer.Ordinal)
            .SequenceEqual(roles.OrderBy(role => role, StringComparer.Ordinal), StringComparer.Ordinal);

        if (rolesChanged)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return UserOperationResult.Failure(
                    stampResult.Errors.Select(error => error.Description),
                    stampResult.Errors.Select(error => error.Code));
            }
        }

        return UserOperationResult.Success();
    }

    public async Task<bool> CheckPasswordAsync(string id, string password, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user is not null && await _userManager.CheckPasswordAsync(user, password);
    }

    private async Task<UserAccount> MapAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserAccount
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
            Roles = roles.ToArray(),
            SecurityStamp = user.SecurityStamp ?? string.Empty
        };
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
