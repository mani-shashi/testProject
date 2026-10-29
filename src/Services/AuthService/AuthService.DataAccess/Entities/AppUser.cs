using Microsoft.AspNetCore.Identity;

namespace AuthService.DataAccess.Entities;

public sealed class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
