using System.ComponentModel.DataAnnotations;
using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Configuration options for application localization.
/// </summary>
/// <remarks>
/// Configure supported languages and default culture in appsettings.json:
/// <code>
/// {
///   "Localization": {
///     "DefaultCulture": "en",
///     "SupportedCultures": ["pt", "en", "fr", "de", "es"]
///   }
/// }
/// </code>
/// The DefaultCulture is used when the client's Accept-Language header doesn't match any supported culture.
/// SupportedCultures defines which languages the API can respond in.
/// </remarks>
public sealed record LocalizationOptions : IValidatableObject
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Localization";

    /// <summary>
    /// Default culture to use when client's preferred language is not supported.
    /// </summary>
    /// <remarks>
    /// Must be a valid culture identifier (e.g., "en", "pt", "es").
    /// Defaults to "en" if not configured.
    /// </remarks>
    [Required(ErrorMessage = "DefaultCulture is required")]
    [MinLength(2, ErrorMessage = "DefaultCulture must be at least 2 characters")]
    [ValidCulture]
    public string DefaultCulture { get; init; } = "en";

    /// <summary>
    /// Array of supported culture identifiers that the API can respond in.
    /// </summary>
    /// <remarks>
    /// Each entry must be a valid culture identifier (e.g., "en", "pt", "es").
    /// The API will match the client's Accept-Language header against this list.
    /// Defaults to ["en"] if not configured.
    /// </remarks>
    [Required(ErrorMessage = "SupportedCultures is required")]
    [MinLength(1, ErrorMessage = "At least one supported culture must be specified")]
    [ValidCulture]
    public required string[] SupportedCultures { get; init; }

    /// <summary>
    /// Validates that DefaultCulture is included in SupportedCultures
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SupportedCultures.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"DefaultCulture '{DefaultCulture}' must be included in SupportedCultures",
                [nameof(DefaultCulture), nameof(SupportedCultures)]);
        }
    }
}
