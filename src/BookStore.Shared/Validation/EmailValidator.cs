using System.ComponentModel.DataAnnotations;

namespace BookStore.Shared.Validation;

/// <summary>
/// Shared email validation logic for consistent validation across the application
/// </summary>
public static class EmailValidator
{
    static readonly EmailAddressAttribute EmailAttribute = new();

    /// <summary>
    /// Validates an email address
    /// </summary>
    /// <param name="email">The email to validate</param>
    /// <returns>True if the email is valid, false otherwise</returns>
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return EmailAttribute.IsValid(email);
    }

    /// <summary>
    /// Returns an error message if the email is invalid, null otherwise
    /// </summary>
    public static string? GetError(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email is required";
        }

        if (!IsValid(email))
        {
            return "Invalid email format";
        }

        return null;
    }
}
