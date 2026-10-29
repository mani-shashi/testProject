using System.ComponentModel.DataAnnotations;

namespace AuthService.Contracts.Requests;

public sealed class AssignRolesRequestDto
{
    [Required]
    [MinLength(1)]
    public List<string> Roles { get; set; } = new();
}
