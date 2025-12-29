using System.Globalization;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Centralized cache for culture validation with efficient lookup
/// </summary>
public static class CultureCache
{
    // Cache culture names for exact matching (e.g., "en-US", "pt-PT")
    static readonly Lazy<HashSet<string>> _cultureNames = new(() =>
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.UserCustomCulture);
        return new HashSet<string>(
            cultures.Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase);
    }, LazyThreadSafetyMode.PublicationOnly);

    // Cache two-letter ISO language codes for language matching (e.g., "en", "pt", "fil")
    static readonly Lazy<HashSet<string>> _isoLanguageCodes = new(() =>
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.UserCustomCulture);
        return new HashSet<string>(
            cultures.Select(c => c.TwoLetterISOLanguageName),
            StringComparer.OrdinalIgnoreCase);
    }, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Validates if a code is a valid culture identifier or ISO language code
    /// </summary>
    /// <param name="code">The code to validate (e.g., "en", "pt", "pt-PT", "fil" for Filipino)</param>
    /// <returns>True if valid, false otherwise</returns>
    /// <remarks>
    /// Accepts:
    /// - Full culture codes (e.g., "pt-PT", "en-US") - contains hyphen
    /// - Two-letter ISO 639-1 codes (e.g., "en", "pt", "fr") - no hyphen, 2 letters
    /// - Three-letter ISO 639-3 codes (e.g., "fil" for Filipino) - no hyphen, 3 letters
    /// </remarks>
    public static bool IsValidCultureCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        // Full culture codes contain a hyphen (e.g., "pt-PT", "en-US")
        if (code.Contains('-'))
        {
            return _cultureNames.Value.Contains(code);
        }

        // ISO language codes without hyphen (2 or 3 letters)
        return _isoLanguageCodes.Value.Contains(code);
    }

    /// <summary>
    /// Validates if a name is a valid culture name (exact match)
    /// </summary>
    /// <param name="name">The culture name to validate (e.g., "en-US", "pt-PT")</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidCultureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _cultureNames.Value.Contains(name);
    }

    /// <summary>
    /// Gets a list of invalid codes from a collection
    /// </summary>
    /// <param name="codes">Collection of codes to validate</param>
    /// <returns>List of invalid codes</returns>
    public static IReadOnlyList<string> GetInvalidCodes(IEnumerable<string> codes)
    {
        var invalidCodes = new List<string>();

        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                invalidCodes.Add("(empty)");
            }
            else if (!IsValidCultureCode(code))
            {
                invalidCodes.Add(code);
            }
        }

        return invalidCodes;
    }
}
