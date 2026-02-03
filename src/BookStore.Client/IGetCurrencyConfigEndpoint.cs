using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Endpoint for retrieving currency configuration
/// </summary>
public interface IGetCurrencyConfigEndpoint
{
    /// <summary>
    /// Get currency configuration (default currency and supported currencies)
    /// </summary>
    [Get("/api/config/currency")]
    Task<CurrencyConfigDto> GetCurrencyConfigAsync();
}
