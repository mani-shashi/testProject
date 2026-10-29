using AuthService.Business.Interfaces;
using AuthService.Business.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.DataAccess.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public RoleRepository(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken)
        => await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);

    public async Task<UserOperationResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var result = await _roleManager.CreateAsync(new IdentityRole(name));
        return result.Succeeded
            ? UserOperationResult.Success()
            : UserOperationResult.Failure(result.Errors.Select(error => error.Description));
    }
}
