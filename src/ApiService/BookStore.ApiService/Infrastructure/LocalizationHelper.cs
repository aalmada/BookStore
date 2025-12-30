using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using BookStore.ApiService.Models;
using Microsoft.AspNetCore.Localization;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Helper methods for localization and culture handling
/// </summary>
public static class LocalizationHelper
{
    /// <summary>
    /// Gets a localized value from a translation dictionary
    /// </summary>
    /// <typeparam name="T">Type of the translation value</typeparam>
    /// <param name="context">The HTTP context</param>
    /// <param name="options">Localization options containing the default culture</param>
    /// <param name="translations">Dictionary of translations with language codes as keys</param>
    /// <param name="selector">Function to extract the string value from T</param>
    /// <param name="defaultValue">Default value to return if no translation is found</param>
    /// <returns>Localized string value or default</returns>
    /// <remarks>
    /// Fallback strategy:
    /// 1. Try the exact preferred language (e.g., "pt-PT")
    /// 2. Try the two-letter code of the preferred language (e.g., "pt")
    /// 3. Try the exact default culture (e.g., "en-US")
    /// 4. Try the two-letter code of the default culture (e.g., "en")
    /// 5. Use the first available translation
    /// 6. Use the default value
    /// </remarks>
    public static string GetLocalizedValue<T>(
        HttpContext context,
        LocalizationOptions options,
        Dictionary<string, T> translations,
        Func<T, string> selector,
        string defaultValue = "")
    {
        // Get preferred culture from middleware or default culture
        var preferredCulture = GetPreferredCulture(context, options);

        // Try preferred culture (exact and two-letter)
        if (TryGetTranslation(translations, preferredCulture, out var preferredTranslation))
        {
            return selector(preferredTranslation);
        }

        // Try default culture (exact and two-letter)
        var defaultCulture = CultureInfo.GetCultureInfo(options.DefaultCulture);
        if (TryGetTranslation(translations, defaultCulture, out var defaultTranslation))
        {
            return selector(defaultTranslation);
        }

        // Use first available translation
        if (translations.Count > 0)
        {
            var first = translations.Values.First();
            return selector(first);
        }

        // Use default value
        return defaultValue;
    }

    /// <summary>
    /// Tries to get a translation for a culture, first by exact name then by two-letter code
    /// </summary>
    static bool TryGetTranslation<T>(
        Dictionary<string, T> translations,
        CultureInfo culture,
        [NotNullWhen(true)] out T? value)
    {
        // Try exact culture name (e.g., "pt-PT")
        if (translations.TryGetValue(culture.Name, out var exact) && exact is not null)
        {
            value = exact;
            return true;
        }

        // Try two-letter code (e.g., "pt")
        if (translations.TryGetValue(culture.TwoLetterISOLanguageName, out var twoLetter) && twoLetter is not null)
        {
            value = twoLetter;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets the localized display name of a language
    /// </summary>
    /// <param name="languageCode">The language code to get the display name for (e.g., "en", "pt")</param>
    /// <param name="context">The HTTP context</param>
    /// <param name="options">Localization options containing the default culture</param>
    /// <returns>Localized language display name</returns>
    /// <remarks>
    /// Returns the display name of the specified language in the user's preferred language.
    /// For example: if languageCode is "en" and user prefers "pt", returns "Inglês"
    /// </remarks>
    public static string LocalizeLanguageName(
        string languageCode,
        HttpContext context,
        LocalizationOptions options)
    {
        // Validate language code first to avoid exceptions
        if (!CultureCache.IsValidCultureCode(languageCode))
        {
            return languageCode.ToUpperInvariant();
        }

        var languageCulture = CultureInfo.GetCultureInfo(languageCode);
        var userCulture = GetPreferredCulture(context, options);

        // Get the display name of the language in the user's language
        // For example: if language is "en" and user prefers "pt", this returns "Inglês"
        return userCulture.TextInfo.ToTitleCase(languageCulture.DisplayName);
    }

    /// <summary>
    /// Gets the user's preferred culture from middleware or default culture
    /// </summary>
    static CultureInfo GetPreferredCulture(HttpContext context, LocalizationOptions options)
    {
        var requestCulture = context.Features.Get<IRequestCultureFeature>();
        return requestCulture is not null
            ? requestCulture.RequestCulture.Culture
            : CultureInfo.GetCultureInfo(options.DefaultCulture);
    }
}
