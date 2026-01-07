using System.Globalization;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Helper class for extracting localized values from translation dictionaries
/// with a comprehensive fallback strategy
/// </summary>
public static class LocalizationHelper
{
    /// <summary>
    /// Gets a localized value from a translations dictionary with fallback strategy:
    /// 1. Exact user culture match (e.g., "en-US")
    /// 2. Two-letter user culture (e.g., "en" from "en-US")
    /// 3. Default culture (e.g., "en-US")
    /// 4. Two-letter default culture (e.g., "en" from "en-US")
    /// 5. Fallback value
    /// </summary>
    /// <param name="translations">Dictionary of translations (key = culture, value = translated text)</param>
    /// <param name="requestedCulture">The culture requested by the user (from Accept-Language)</param>
    /// <param name="defaultCulture">The default culture for the application</param>
    /// <param name="fallback">Fallback value if no translation found</param>
    /// <returns>Localized value or fallback</returns>
    public static string GetLocalizedValue(
        Dictionary<string, string> translations,
        string requestedCulture,
        string defaultCulture,
        string fallback = "")
    {
        if (translations == null || translations.Count == 0)
        {
            return fallback;
        }

        // 1. Exact user culture match (e.g., "en-US", "pt-PT")
        if (translations.TryGetValue(requestedCulture, out var exact))
        {
            return exact;
        }

        // 2. Two-letter user culture (e.g., "en" from "en-US")
        try
        {
            var userNeutral = new CultureInfo(requestedCulture).TwoLetterISOLanguageName;
            if (translations.TryGetValue(userNeutral, out var neutralValue))
            {
                return neutralValue;
            }
        }
        catch (CultureNotFoundException)
        {
            // Invalid culture, continue to fallback
        }

        // 3. Default culture (e.g., "en-US")
        if (translations.TryGetValue(defaultCulture, out var def))
        {
            return def;
        }

        // 4. Two-letter default culture (e.g., "en" from "en-US")
        try
        {
            var defaultNeutral = new CultureInfo(defaultCulture).TwoLetterISOLanguageName;
            if (translations.TryGetValue(defaultNeutral, out var defNeutral))
            {
                return defNeutral;
            }
        }
        catch (CultureNotFoundException)
        {
            // Invalid culture, continue to fallback
        }

        // 5. Fallback value
        return fallback;
    }
}
