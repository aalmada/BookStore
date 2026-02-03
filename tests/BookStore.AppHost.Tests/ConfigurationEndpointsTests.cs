using System.Net;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class ConfigurationEndpointsTests
{
    [Test]
    public async Task GetLocalizationConfig_ShouldReturnConfiguration()
    {
        // Arrange
        var client = TestHelpers.GetUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/config/localization");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<LocalizationConfigDto>();
        _ = await Assert.That(config).IsNotNull();
        _ = await Assert.That(config!.DefaultCulture).IsNotNullOrEmpty();
        _ = await Assert.That(config.SupportedCultures).IsNotEmpty();
        _ = await Assert.That(config.SupportedCultures).Contains(config.DefaultCulture);
    }

    [Test]
    public async Task GetCurrencyConfig_ShouldReturnConfiguration()
    {
        // Arrange
        var client = TestHelpers.GetUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/config/currency");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<CurrencyConfigDto>();
        _ = await Assert.That(config).IsNotNull();
        _ = await Assert.That(config!.DefaultCurrency).IsNotNullOrEmpty();
        _ = await Assert.That(config.SupportedCurrencies).IsNotEmpty();
        _ = await Assert.That(config.SupportedCurrencies).Contains(config.DefaultCurrency);
    }

    [Test]
    public async Task GetLocalizationConfig_ShouldBeAccessibleWithoutAuthentication()
    {
        // Arrange
        var client = TestHelpers.GetUnauthenticatedClient();
        // No authentication headers added

        // Act
        var response = await client.GetAsync("/api/config/localization");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetCurrencyConfig_ShouldBeAccessibleWithoutAuthentication()
    {
        // Arrange
        var client = TestHelpers.GetUnauthenticatedClient();
        // No authentication headers added

        // Act
        var response = await client.GetAsync("/api/config/currency");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetLocalizationConfig_ShouldMatchAppSettings()
    {
        // Arrange
        var client = TestHelpers.GetUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/config/localization");
        var config = await response.Content.ReadFromJsonAsync<LocalizationConfigDto>();

        // Assert
        _ = await Assert.That(config).IsNotNull();
        // Based on appsettings.json defaults
        _ = await Assert.That(config!.DefaultCulture).IsEqualTo("en");
        _ = await Assert.That(config.SupportedCultures).Contains("pt");
        _ = await Assert.That(config.SupportedCultures).Contains("pt-PT");
        _ = await Assert.That(config.SupportedCultures).Contains("en");
        _ = await Assert.That(config.SupportedCultures).Contains("fr");
        _ = await Assert.That(config.SupportedCultures).Contains("de");
        _ = await Assert.That(config.SupportedCultures).Contains("es");
    }

    [Test]
    public async Task GetCurrencyConfig_ShouldMatchAppSettings()
    {
        // Arrange
        var client = TestHelpers.GetUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/config/currency");
        var config = await response.Content.ReadFromJsonAsync<CurrencyConfigDto>();

        // Assert
        _ = await Assert.That(config).IsNotNull();
        // Based on appsettings.json defaults
        _ = await Assert.That(config!.DefaultCurrency).IsEqualTo("USD");
        _ = await Assert.That(config.SupportedCurrencies).Contains("USD");
        _ = await Assert.That(config.SupportedCurrencies).Contains("EUR");
        _ = await Assert.That(config.SupportedCurrencies).Contains("GBP");
    }
}
