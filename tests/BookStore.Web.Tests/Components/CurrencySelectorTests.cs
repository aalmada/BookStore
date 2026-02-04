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
    IJSRuntime _jsRuntime = null!;
    CurrencyService _currencyService = null!;

    [Before(Test)]
    public void Setup()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
        _currencyService = new CurrencyService(_jsRuntime);

        _ = Context.Services.AddSingleton(_currencyService);
    }

    [Test]
    public async Task CurrencySelector_ShouldRenderCurrentCurrency()
    {
        // Act
        var cut = RenderComponent<CurrencySelector>();

        // Assert
        // The button text should contain the default currency (USD)
        var button = cut.Find("button");
        _ = await Assert.That(button.TextContent).Contains("USD");
    }

    [Test]
    public async Task CurrencySelector_ShouldChangeCurrency_OnClick()
    {
        // Arrange
        var cut = RenderComponent<CurrencySelector>();

        // Act
        await cut.InvokeAsync(async () => await _currencyService.SetCurrencyAsync("EUR"));

        // Assert
        var button = cut.Find("button");
        _ = await Assert.That(button.TextContent).Contains("EUR");
    }
}
