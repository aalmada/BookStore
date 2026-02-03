using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for retrieving application configuration
/// </summary>
public interface IConfigurationClient :
    IGetLocalizationConfigEndpoint,
    IGetCurrencyConfigEndpoint
{
}
