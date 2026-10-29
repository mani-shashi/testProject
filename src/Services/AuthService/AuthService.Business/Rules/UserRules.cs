using AuthService.Business.Authorization;
using AuthService.Business.Exceptions;

namespace AuthService.Business.Rules;

public static class UserRules
{
    public static IReadOnlyList<string> NormalizeRoles(IReadOnlyCollection<string>? roles)
    {
        if (roles is null || roles.Count == 0)
        {
            throw new ValidationException("At least one role is required.");
        }

        var normalized = new List<string>();

        foreach (var role in roles)
        {
            var match = Roles.Normalize(role.Trim());
            if (match is null)
            {
                throw new ValidationException($"Unknown role '{role}'. Allowed roles: {string.Join(", ", Roles.All)}.");
            }

            if (!normalized.Contains(match))
            {
                normalized.Add(match);
            }
        }

        return normalized;
    }

    public static void EnsureNotSelf(string id, string currentUserId, string action)
    {
        if (string.Equals(id, currentUserId, StringComparison.Ordinal))
        {
            throw new ValidationException($"You cannot {action} your own account.");
        }
    }

    public static void EnsureOwnerRoleRetained(string id, string currentUserId, IReadOnlyCollection<string> roles)
    {
        if (string.Equals(id, currentUserId, StringComparison.Ordinal) && !roles.Contains(Roles.Owner))
        {
            throw new ValidationException("You cannot remove your own Owner role.");
        }
    }
}
