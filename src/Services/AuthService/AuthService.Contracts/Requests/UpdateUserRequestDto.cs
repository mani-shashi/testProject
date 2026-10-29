using System.ComponentModel.DataAnnotations;

namespace AuthService.Contracts.Requests;

public sealed class UpdateUserRequestDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
