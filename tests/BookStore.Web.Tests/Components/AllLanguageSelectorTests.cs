using BookStore.Client;
using BookStore.Web.Components.Shared;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class AllLanguageSelectorTests : BunitTestContext
{
    IConfigurationClient _configurationClient = null!;
    LanguageService _languageService = null!;

    [Before(Test)]
    public void Setup()
    {
        _configurationClient = Substitute.For<IConfigurationClient>();
        _languageService = new LanguageService(_configurationClient);

        _ = Context.Services.AddSingleton(_languageService);
    }

    [Test]
    public async Task AllLanguageSelector_ShouldRenderWithCurrentValue()
    {
        // Act
        var cut = RenderComponent<AllLanguageSelector>(parameters => parameters
            .Add<string?>(p => p.Value, "pt-PT")
            .Add<string>(p => p.Label, "Primary Language")
        );

        // Assert
        _ = await Assert.That(cut.Find("label").TextContent).IsEqualTo("Primary Language");
        var input = cut.Find("input");
        _ = await Assert.That(input.GetAttribute("value")).IsEqualTo("pt-PT");
    }

    [Test]
    public async Task AllLanguageSelector_ShouldTriggerValueChanged_OnSelection()
    {
        // Arrange
        string? selectedValue = null;
        var cut = RenderComponent<AllLanguageSelector>(parameters => parameters
            .Add<string?>(p => p.Value, "en-US")
            .Add<EventCallback<string>>(p => p.ValueChanged,
                EventCallback.Factory.Create<string>(this, v => selectedValue = v))
        );

        // Act
        cut.Find("input").Change("pt-PT");

        // Assert
        _ = await Assert.That(selectedValue).IsEqualTo("pt-PT");
    }

    // For Search tests in bUnit with MudAutocomplete, it's often better to test 
    // the underlying service or a simplified version, but we've verified 
    // the service in LanguageServiceTests.cs.
}
