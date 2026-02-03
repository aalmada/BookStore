using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Endpoint for retrieving localization configuration
/// </summary>
public interface IGetLocalizationConfigEndpoint
{
    /// <summary>
    /// Get localization configuration (default culture and supported cultures)
    /// </summary>
    [Get("/api/config/localization")]
    Task<LocalizationConfigDto> GetLocalizationConfigAsync();
}
