namespace AuthService.Business.Models;

public sealed class UserAccount
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string SecurityStamp { get; init; } = string.Empty;
}

public sealed record UserTokenStatus(string Id, bool IsActive, string SecurityStamp);

public sealed record UserOperationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> ErrorCodes)
{
    public static UserOperationResult Success() => new(true, Array.Empty<string>(), Array.Empty<string>());

    public static UserOperationResult Failure(IEnumerable<string> errors, IEnumerable<string>? errorCodes = null)
        => new(false, errors.ToArray(), errorCodes?.ToArray() ?? Array.Empty<string>());
}
