using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Components.Shared;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class CurrencySelectorTests : BunitTestContext
{
    IConfigurationClient _configurationClient = null!;
    IJSRuntime _jsRuntime = null!;
    CurrencyService _currencyService = null!;

    [Before(Test)]
    public void Setup()
    {
        _configurationClient = Substitute.For<IConfigurationClient>();
        _jsRuntime = Substitute.For<IJSRuntime>();
        _currencyService = new CurrencyService(_jsRuntime);

        _ = _configurationClient.GetCurrencyConfigAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrencyConfigDto("GBP", ["GBP", "EUR", "USD"]));

        _ = Context.Services.AddSingleton(_configurationClient);
        _ = Context.Services.AddSingleton(_currencyService);
    }

    [Test]
    public async Task CurrencySelector_ShouldRenderCurrentCurrency()
    {
        // Act
        var cut = RenderComponent<CurrencySelector>();

        // Assert
        var input = cut.Find("input");
        _ = await Assert.That(input.GetAttribute("value")).IsEqualTo("GBP");
    }

    [Test]
    public async Task CurrencySelector_ShouldChangeCurrency_OnClick()
    {
        // Arrange
        var cut = RenderComponent<CurrencySelector>();

        // Act
        await cut.InvokeAsync(async () => await _currencyService.SetCurrencyAsync("EUR"));

        // Assert
        var input = cut.Find("input");
        _ = await Assert.That(input.GetAttribute("value")).IsEqualTo("EUR");
    }
}
