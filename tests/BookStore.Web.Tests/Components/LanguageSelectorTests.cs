using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Components.Shared;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class LanguageSelectorTests : BunitTestContext
{
    IConfigurationClient _configurationClient = null!;
    LanguageService _languageService = null!;

    [Before(Test)]
    public void Setup()
    {
        _configurationClient = Substitute.For<IConfigurationClient>();
        _languageService = new LanguageService(_configurationClient);

        _ = Context.Services.AddSingleton(_languageService);

        // Mocking the backend response for supported languages
        _ = _configurationClient.GetLocalizationConfigAsync()
            .Returns(new LocalizationConfigDto("en-US", ["en-US", "pt-PT"]));
    }

    [Test]
    public async Task LanguageSelector_ShouldRenderWithSupportedLanguages()
    {
        // Act
        var cut = RenderComponent<LanguageSelector>(parameters => parameters
            .Add<string?>(p => p.Value, "en-US")
            .Add<string>(p => p.Label, "Test Label")
        );

        // Assert
        _ = await Assert.That(cut.Find("label").TextContent).IsEqualTo("Test Label");
        var select = cut.Find("select");
        _ = await Assert.That(select.GetAttribute("value")).IsEqualTo("en-US");
    }

    [Test]
    public async Task LanguageSelector_ShouldTriggerValueChanged_OnSelection()
    {
        // Arrange
        string? selectedValue = null;
        var cut = RenderComponent<LanguageSelector>(parameters => parameters
            .Add<string?>(p => p.Value, "en-US")
            .Add(p => p.ValueChanged,
                EventCallback.Factory.Create<string>(this, v => selectedValue = v))
        );

        // Act
        cut.Find("select").Change("pt-PT");

        // Assert
        _ = await Assert.That(selectedValue).IsEqualTo("pt-PT");
    }
}
