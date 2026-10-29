using AuthService.Contracts.DTOs;

namespace AuthService.Contracts.Responses;

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public UserResponseDto User { get; set; } = new();
}
