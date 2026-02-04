using BookStore.Client;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing supported languages configuration
/// </summary>
public class LanguageService
{
    private readonly IConfigurationClient _configurationClient;
    private string[]? _cachedLanguages;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Comprehensive mapping of culture codes to display names
    private static readonly Dictionary<string, string> CultureDisplayNames = new()
    {
        { "en", "English" },
        { "en-US", "English (United States)" },
        { "en-GB", "English (United Kingdom)" },
        { "pt", "Portuguese" },
        { "pt-PT", "Portuguese (Portugal)" },
        { "pt-BR", "Portuguese (Brazil)" },
        { "es", "Spanish" },
        { "es-ES", "Spanish (Spain)" },
        { "es-MX", "Spanish (Mexico)" },
        { "fr", "French" },
        { "fr-FR", "French (France)" },
        { "de", "German" },
        { "de-DE", "German (Germany)" },
        { "it", "Italian" },
        { "it-IT", "Italian (Italy)" },
        { "ja", "Japanese" },
        { "ja-JP", "Japanese (Japan)" },
        { "zh", "Chinese" },
        { "zh-CN", "Chinese (Simplified)" },
        { "zh-TW", "Chinese (Traditional)" },
        { "ko", "Korean" },
        { "ko-KR", "Korean (Korea)" },
        { "ru", "Russian" },
        { "ru-RU", "Russian (Russia)" },
        { "ar", "Arabic" },
        { "ar-SA", "Arabic (Saudi Arabia)" },
        { "nl", "Dutch" },
        { "nl-NL", "Dutch (Netherlands)" },
        { "pl", "Polish" },
        { "pl-PL", "Polish (Poland)" },
        { "sv", "Swedish" },
        { "sv-SE", "Swedish (Sweden)" },
        { "tr", "Turkish" },
        { "tr-TR", "Turkish (Turkey)" },
    };

    public LanguageService(IConfigurationClient configurationClient)
    {
        _configurationClient = configurationClient;
    }

    /// <summary>
    /// Get supported languages from the backend configuration
    /// </summary>
    public async Task<string[]> GetSupportedLanguagesAsync()
    {
        if (_cachedLanguages != null)
        {
            return _cachedLanguages;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cachedLanguages != null)
            {
                return _cachedLanguages;
            }

            var config = await _configurationClient.GetLocalizationConfigAsync();
            _cachedLanguages = config.SupportedCultures.ToArray();
            return _cachedLanguages;
        }
        catch
        {
            // Fallback to default languages if API call fails
            _cachedLanguages = ["en", "pt", "pt-PT", "es", "fr", "de"];
            return _cachedLanguages;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get display name for a culture code
    /// </summary>
    public static string GetDisplayName(string cultureCode)
    {
        return CultureDisplayNames.TryGetValue(cultureCode, out var displayName)
            ? displayName
            : cultureCode; // Fallback to culture code if no mapping exists
    }

    /// <summary>
    /// Get all supported languages with their display names
    /// </summary>
    public async Task<Dictionary<string, string>> GetLanguagesWithDisplayNamesAsync()
    {
        var languages = await GetSupportedLanguagesAsync();
        return languages
            .Distinct() // Remove duplicates
            .ToDictionary(
                lang => lang,
                lang => GetDisplayName(lang)
            );
    }

    /// <summary>
    /// Get the default culture from the backend configuration
    /// </summary>
    public async Task<string> GetDefaultCultureAsync()
    {
        try
        {
            var config = await _configurationClient.GetLocalizationConfigAsync();
            return config.DefaultCulture;
        }
        catch
        {
            // Fallback to "en" if API call fails
            return "en";
        }
    }

    /// <summary>
    /// Clear the cached languages (useful for testing or if configuration changes)
    /// </summary>
    public void ClearCache()
    {
        _cachedLanguages = null;
    }
}
