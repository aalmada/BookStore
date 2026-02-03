using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

/// <summary>
/// Endpoints for retrieving application configuration
/// </summary>
public static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
    {
        // Public endpoints for configuration - no authentication required
        _ = group.WithMetadata(new AllowAnonymousTenantAttribute());

        _ = group.MapGet("/localization", GetLocalizationConfig)
            .WithName("GetLocalizationConfig")
            .WithSummary("Get localization configuration")
            .WithDescription("Returns the default culture and supported cultures configured in the application");

        _ = group.MapGet("/currency", GetCurrencyConfig)
            .WithName("GetCurrencyConfig")
            .WithSummary("Get currency configuration")
            .WithDescription("Returns the default currency and supported currencies configured in the application");

        return group;
    }

    /// <summary>
    /// GET /api/config/localization
    /// Returns the localization configuration (default culture and supported cultures)
    /// </summary>
    public static async Task<IResult> GetLocalizationConfig(
        [FromServices] HybridCache cache,
        IOptions<LocalizationOptions> localizationOptions,
        CancellationToken cancellationToken = default)
    {
        var response = await cache.GetOrCreateAsync(
            "config:localization",
            async cancel =>
            {
                var options = localizationOptions.Value;
                return new LocalizationConfigDto(
                    options.DefaultCulture,
                    options.SupportedCultures
                );
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24), // Configuration rarely changes
                LocalCacheExpiration = TimeSpan.FromHours(1)
            },
            cancellationToken: cancellationToken
        );

        return Results.Ok(response);
    }

    /// <summary>
    /// GET /api/config/currency
    /// Returns the currency configuration (default currency and supported currencies)
    /// </summary>
    public static async Task<IResult> GetCurrencyConfig(
        [FromServices] HybridCache cache,
        IOptions<CurrencyOptions> currencyOptions,
        CancellationToken cancellationToken = default)
    {
        var response = await cache.GetOrCreateAsync(
            "config:currency",
            async cancel =>
            {
                var options = currencyOptions.Value;
                return new CurrencyConfigDto(
                    options.DefaultCurrency,
                    options.SupportedCurrencies
                );
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24), // Configuration rarely changes
                LocalCacheExpiration = TimeSpan.FromHours(1)
            },
            cancellationToken: cancellationToken
        );

        return Results.Ok(response);
    }
}
