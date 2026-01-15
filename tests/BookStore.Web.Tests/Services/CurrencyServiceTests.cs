using BookStore.Web.Services;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Services;

public class CurrencyServiceTests
{
    IJSRuntime _jsRuntime = null!;
    CurrencyService _sut = null!;

    [Before(Test)]
    public void Setup()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
        _sut = new CurrencyService(_jsRuntime);
    }

    [Test]
    public async Task InitializeAsync_ShouldLoadFromLocalStorage()
    {
        // Arrange
        _ = _jsRuntime.InvokeAsync<string?>("localStorage.getItem", Arg.Is<object[]>(a => a[0].ToString() == "selected_currency"))
            .Returns(new ValueTask<string?>("EUR"));

        // Act
        await _sut.InitializeAsync();

        // Assert
        _ = await Assert.That(_sut.CurrentCurrency).IsEqualTo("EUR");
    }

    [Test]
    public async Task SetCurrencyAsync_ShouldUpdateStateAndLocalStorage()
    {
        // Act
        await _sut.SetCurrencyAsync("GBP");

        // Assert
        _ = await Assert.That(_sut.CurrentCurrency).IsEqualTo("GBP");
        _ = await _jsRuntime.Received(1).InvokeAsync<IJSVoidResult>("localStorage.setItem", Arg.Is<object[]>(a => a[0].ToString() == "selected_currency" && a[1].ToString() == "GBP"));
    }

    [Test]
    public async Task SetCurrencyAsync_ShouldTriggerEvent()
    {
        // Arrange
        var eventTriggered = false;
        _sut.OnCurrencyChanged += () => eventTriggered = true;

        // Act
        await _sut.SetCurrencyAsync("EUR");

        // Assert
        _ = await Assert.That(eventTriggered).IsTrue();
    }

    [Test]
    [Arguments("USD", "$10.00")]
    [Arguments("EUR", "10.00€")]
    [Arguments("GBP", "£10.00")]
    public async Task FormatPrice_ShouldReturnCorrectFormat(string currency, string expected)
    {
        // Arrange
        await _sut.SetCurrencyAsync(currency);
        var prices = new Dictionary<string, decimal>
        {
            ["USD"] = 10.00m,
            ["EUR"] = 10.00m,
            ["GBP"] = 10.00m
        };

        // Act
        var result = _sut.FormatPrice(prices);

        // Assert
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task FormatPrice_ShouldReturnNA_WhenCurrencyMissing()
    {
        // Arrange
        await _sut.SetCurrencyAsync("EUR");
        var prices = new Dictionary<string, decimal>
        {
            ["USD"] = 10.00m
        };

        // Act
        var result = _sut.FormatPrice(prices);

        // Assert
        _ = await Assert.That(result).IsEqualTo("N/A");
    }
}
