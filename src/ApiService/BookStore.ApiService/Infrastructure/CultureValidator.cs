using System.Globalization;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Helper methods for validating culture/language codes
/// </summary>
public static class CultureValidator
{
    // Cache all cultures to avoid repeated allocations
    static readonly CultureInfo[] AllCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

    /// <summary>
    /// Validates if a language code is a valid culture identifier or ISO language code
    /// </summary>
    /// <param name="languageCode">The language code to validate (e.g., "en", "pt", "pt-PT", "fil" for Filipino)</param>
    /// <returns>True if valid, false otherwise</returns>
    /// <remarks>
    /// Accepts:
    /// - Full culture codes (e.g., "pt-PT", "en-US") - contains hyphen
    /// - Two-letter ISO 639-1 codes (e.g., "en", "pt", "fr") - no hyphen, 2 letters
    /// - Three-letter ISO 639-3 codes (e.g., "fil" for Filipino) - no hyphen, 3 letters
    /// </remarks>
    public static bool IsValidCultureCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        // Full culture codes contain a hyphen (e.g., "pt-PT", "en-US")
        if (languageCode.Contains('-'))
        {
            // Check if any culture has this exact name (avoids allocation and exception)
            return AllCultures.Any(c => c.Name.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
        }

        // ISO language codes without hyphen (2 or 3 letters)
        // ISO 639-1: 2 letters (e.g., "en", "pt")
        // ISO 639-3: 3 letters (e.g., "fil" for Filipino)
        return AllCultures.Any(c => c.TwoLetterISOLanguageName.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates a dictionary of translations, ensuring all keys are valid culture codes or ISO language codes
    /// </summary>
    /// <param name="translations">Dictionary of translations with language codes as keys</param>
    /// <param name="invalidCodes">Output parameter containing list of invalid codes found</param>
    /// <returns>True if all codes are valid, false otherwise</returns>
    public static bool ValidateTranslations<T>(
        Dictionary<string, T> translations,
        out List<string> invalidCodes)
    {
        invalidCodes = [];

        foreach (var key in translations.Keys)
        {
            if (!IsValidCultureCode(key))
            {
                invalidCodes.Add(key);
            }
        }

        return invalidCodes.Count == 0;
    }
}
