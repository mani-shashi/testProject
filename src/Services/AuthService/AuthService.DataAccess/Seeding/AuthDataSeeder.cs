using AuthService.Business.Authorization;
using AuthService.Business.Configuration;
using AuthService.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.DataAccess.Seeding;

public static class AuthDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, BootstrapOwnerSettings settings)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{role}': {string.Join(" ", roleResult.Errors.Select(error => error.Description))}");
            }
        }

        var email = settings.OwnerEmail.Trim().ToLowerInvariant();
        var owner = await userManager.FindByEmailAsync(email);

        if (owner is null)
        {
            owner = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = settings.OwnerFullName,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(owner, settings.OwnerPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the bootstrap Owner: {string.Join(" ", createResult.Errors.Select(error => error.Description))}");
            }
        }
        else if (!owner.IsActive)
        {
            owner.IsActive = true;
            var updateResult = await userManager.UpdateAsync(owner);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not reactivate the bootstrap Owner: {string.Join(" ", updateResult.Errors.Select(error => error.Description))}");
            }

            var stampResult = await userManager.UpdateSecurityStampAsync(owner);
            if (!stampResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not refresh the bootstrap Owner security stamp: {string.Join(" ", stampResult.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(owner, Roles.Owner))
        {
            var roleResult = await userManager.AddToRoleAsync(owner, Roles.Owner);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign the Owner role: {string.Join(" ", roleResult.Errors.Select(error => error.Description))}");
            }
        }
    }
}
