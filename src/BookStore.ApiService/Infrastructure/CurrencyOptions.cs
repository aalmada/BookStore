using System.ComponentModel.DataAnnotations;
using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Configuration options for application currencies.
/// </summary>
/// <remarks>
/// Configure supported currencies and default currency in appsettings.json:
/// <code>
/// {
///   "Currency": {
///     "DefaultCurrency": "USD",
///     "SupportedCurrencies": ["USD", "EUR", "GBP"]
///   }
/// }
/// </code>
/// </remarks>
public sealed record CurrencyOptions : IValidatableObject
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Currency";

    /// <summary>
    /// Default currency to use.
    /// </summary>
    /// <remarks>
    /// Must be a valid ISO 4217 currency code (e.g., "USD", "EUR").
    /// Defaults to "USD" if not configured.
    /// </remarks>
    [Required(ErrorMessage = "DefaultCurrency is required")]
    [Length(3, 3, ErrorMessage = "DefaultCurrency must be a 3-character ISO code")]
    public string DefaultCurrency { get; init; } = "USD";

    /// <summary>
    /// Array of supported ISO 4217 currency identifiers.
    /// </summary>
    [Required(ErrorMessage = "SupportedCurrencies is required")]
    [MinLength(1, ErrorMessage = "At least one supported currency must be specified")]
    public required string[] SupportedCurrencies { get; init; }

    /// <summary>
    /// Validates that DefaultCurrency is included in SupportedCurrencies
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SupportedCurrencies.Contains(DefaultCurrency, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"DefaultCurrency '{DefaultCurrency}' must be included in SupportedCurrencies",
                [nameof(DefaultCurrency), nameof(SupportedCurrencies)]);
        }
    }
}
