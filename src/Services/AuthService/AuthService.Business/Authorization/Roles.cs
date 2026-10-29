namespace AuthService.Business.Authorization;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Receptionist = "Receptionist";

    public static readonly IReadOnlyList<string> All = new[] { Owner, Manager, Receptionist };

    public static string? Normalize(string role)
        => All.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
