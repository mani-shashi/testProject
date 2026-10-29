using AuthService.Business.Models;
using AuthService.Contracts.DTOs;

namespace AuthService.Business.Mapping;

public static class UserMapper
{
    public static UserResponseDto ToDto(UserAccount user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        IsActive = user.IsActive,
        Roles = user.Roles,
        CreatedAtUtc = user.CreatedAtUtc
    };
}
