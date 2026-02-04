using System.Globalization;
using BookStore.Client;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing supported languages configuration
/// </summary>
public class LanguageService
{
    readonly IConfigurationClient _configurationClient;
    string[]? _cachedLanguages;
    readonly SemaphoreSlim _lock = new(1, 1);

    public LanguageService(IConfigurationClient configurationClient) => _configurationClient = configurationClient;

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
            _cachedLanguages = [.. config.SupportedCultures];
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
            _ = _lock.Release();
        }
    }

    /// <summary>
    /// Get display name for a culture code in the current UI language
    /// </summary>
    public static string GetDisplayName(string cultureCode)
    {
        try
        {
            var culture = new CultureInfo(cultureCode);
            return culture.DisplayName;
        }
        catch
        {
            return cultureCode;
        }
    }

    /// <summary>
    /// Get all supported languages with their display names in the current UI language
    /// </summary>
    public async Task<Dictionary<string, string>> GetLanguagesWithDisplayNamesAsync()
        => (await GetSupportedLanguagesAsync())
        .Distinct() // Remove duplicates
        .ToDictionary(
            lang => lang,
            GetDisplayName
        );

    /// <summary>
    /// Get all available .NET languages with their display names in the current UI language
    /// </summary>
    public IEnumerable<(string Code, string LocalName, string NativeName)> GetAllLanguages() => CultureInfo.GetCultures(CultureTypes.NeutralCultures | CultureTypes.SpecificCultures)
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .OrderBy(c => c.DisplayName)
            .Select(c => (c.Name, c.DisplayName, c.NativeName));

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
    public void ClearCache() => _cachedLanguages = null;
}
