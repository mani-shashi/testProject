using System.ComponentModel.DataAnnotations;

namespace AuthService.Contracts.Requests;

public sealed class CreateUserRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> Roles { get; set; } = new();
}
