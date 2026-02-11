using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for retrieving application configuration
/// </summary>
public interface IConfigurationClient
{
    /// <summary>
    /// Get localization configuration (default culture and supported cultures)
    /// </summary>
    [Get("/api/config/localization")]
    Task<LocalizationConfigDto> GetLocalizationConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get currency configuration (default currency and supported currencies).
    /// </summary>
    [Get("/api/config/currency")]
    Task<CurrencyConfigDto> GetCurrencyConfigAsync(CancellationToken cancellationToken = default);
}

