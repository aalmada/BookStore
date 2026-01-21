namespace BookStore.Shared.Validation;

/// <summary>
/// Shared password validation logic for consistent validation across frontend and backend
/// </summary>
public static class PasswordValidator
{
    /// <summary>
    /// Minimum required password length
    /// </summary>
    public const int MinLength = 8;

    /// <summary>
    /// Validates a password against security requirements
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>A tuple containing whether the password is valid and any validation errors</returns>
    public static (bool IsValid, IReadOnlyList<string> Errors) Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required");
            return (false, errors);
        }

        if (password.Length < MinLength)
        {
            errors.Add($"At least {MinLength} characters");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("At least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("At least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("At least one number");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            errors.Add("At least one special character");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Returns a single error message for display (first error or null if valid)
    /// </summary>
    public static string? GetFirstError(string? password)
    {
        var (_, errors) = Validate(password);
        return errors.FirstOrDefault();
    }
}
