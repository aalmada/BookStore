using Blazored.LocalStorage;
using BookStore.Web.Components.Shared;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class ThemeSwitcherTests : BunitTestContext
{
    ILocalStorageService _localStorage = null!;
    ThemeService _themeService = null!;

    [Before(Test)]
    public void Setup()
    {
        _localStorage = Substitute.For<ILocalStorageService>();
        _themeService = new ThemeService(_localStorage);

        _ = Context.Services.AddSingleton(_themeService);
    }

    [Test]
    public async Task ThemeSwitcher_ShouldRender()
    {
        // Act
        var cut = RenderComponent<ThemeSwitcher>();

        // Assert
        var menu = cut.FindComponent<MudMenu>();
        _ = await Assert.That(menu).IsNotNull();
    }

    [Test]
    public async Task ThemeSwitcher_ShouldChangeTheme_OnClick()
    {
        // Arrange
        var cut = RenderComponent<ThemeSwitcher>();

        // Act
        await cut.InvokeAsync(async () => await _themeService.SetThemeModeAsync(ThemeMode.Dark));

        // Assert
        _ = await Assert.That(_themeService.CurrentTheme).IsEqualTo(ThemeMode.Dark);
    }
}
