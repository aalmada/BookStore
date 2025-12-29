namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Helper methods for validating culture/language codes
/// </summary>
public static class CultureValidator
{
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
        => CultureCache.IsValidCultureCode(languageCode);

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
        var invalid = CultureCache.GetInvalidCodes(translations.Keys);
        invalidCodes = [.. invalid];
        return invalidCodes.Count == 0;
    }
}
