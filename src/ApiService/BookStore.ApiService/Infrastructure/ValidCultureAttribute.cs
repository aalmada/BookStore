using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Validates that a string is a valid culture identifier
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidCultureAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidCultureAttribute"/> class
    /// </summary>
    public ValidCultureAttribute()
        : base("The value '{0}' is not a valid culture identifier")
    {
    }

    /// <summary>
    /// Validates that the value is a valid culture identifier
    /// </summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is string culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return new ValidationResult(
                    "Culture identifier cannot be empty",
                    [validationContext.MemberName!]);
            }

            if (!CultureCache.IsValidCultureName(culture))
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    [validationContext.MemberName!]);
            }

            return ValidationResult.Success;
        }

        if (value is IEnumerable<string> cultures)
        {
            var invalidCodes = CultureCache.GetInvalidCodes(cultures);

            if (invalidCodes.Count > 0)
            {
                return new ValidationResult(
                    $"The following culture identifiers are invalid: {string.Join(", ", invalidCodes)}",
                    [validationContext.MemberName!]);
            }

            return ValidationResult.Success;
        }

        return new ValidationResult(
            $"The value must be a string or IEnumerable<string>, but was {value.GetType().Name}",
            [validationContext.MemberName!]);
    }
}
