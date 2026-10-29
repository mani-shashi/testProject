namespace AuthService.Business.Configuration;

public sealed class BootstrapOwnerSettings
{
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerPassword { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = "Hotel Owner";
}
