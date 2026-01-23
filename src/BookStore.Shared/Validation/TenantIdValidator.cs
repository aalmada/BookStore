using System.Collections.Generic;
using System.Linq;

namespace BookStore.Shared.Validation;

/// <summary>
/// Shared tenant ID validation logic for consistent validation across frontend and backend.
/// Tenant IDs are used in URLs, so they must be URL-friendly.
/// </summary>
public static class TenantIdValidator
{
    public const int MinLength = 3;

    public static (bool IsValid, IReadOnlyList<string> Errors) Validate(string? id)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add("Tenant ID is required");
            return (false, errors);
        }

        if (id.Length < MinLength)
        {
            errors.Add($"At least {MinLength} characters");
        }

        if (id.Any(char.IsUpper))
        {
            errors.Add("Only lowercase letters");
        }

        if (id.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-'))
        {
            errors.Add("Only letters, numbers, and hyphens");
        }

        if (id.Any(char.IsWhiteSpace))
        {
            errors.Add("No spaces allowed");
        }

        if (id.StartsWith('-'))
        {
            errors.Add("Cannot start with a hyphen");
        }

        if (id.EndsWith('-'))
        {
            errors.Add("Cannot end with a hyphen");
        }

        // Standard regex-like check for "only lowercase alphanumeric and hyphens"
        // and ensure it's not JUST symbols if that's a concern (though hyphens are allowed)

        return (errors.Count == 0, errors);
    }

    public static string? GetFirstError(string? id)
    {
        var (_, errors) = Validate(id);
        return errors.FirstOrDefault();
    }
}
