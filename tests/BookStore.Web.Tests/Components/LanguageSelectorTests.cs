using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Components.Shared;
using BookStore.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
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
        _ = Context.Services.AddMudServices();

        // Mocking the backend response for supported languages
        _ = _configurationClient.GetLocalizationConfigAsync()
            .Returns(new LocalizationConfigDto("en-US", ["en-US", "pt-PT"]));
    }

    [Test]
    public async Task LanguageSelector_ShouldRenderWithSupportedLanguages()
    {
        // Act
        var cut = RenderComponent<LanguageSelector>(parameters => parameters
            .Add(p => p.Value, "en-US")
            .Add(p => p.Label, "Test Label")
        );

        // Assert
        _ = await Assert.That(cut.Find(".mud-input-label").TextContent).IsEqualTo("Test Label");
        // MudSelect might need more complex interaction to verify items, 
        // but it should at least render the label and the current value.
        var input = cut.Find("input");
        _ = await Assert.That(input.GetAttribute("value")).IsEqualTo("English (United States) (Default)");
    }

    [Test]
    public async Task LanguageSelector_ShouldTriggerValueChanged_OnSelection()
    {
        // Arrange
        string? selectedValue = null;
        var cut = RenderComponent<LanguageSelector>(parameters => parameters
            .Add(p => p.Value, "en-US")
            .Add<EventCallback<string>>(p => p.ValueChanged,
                EventCallback.Factory.Create<string>(this, v => selectedValue = v))
        );

        // Act - Invoke the value change directly since UI interaction is flaky in bUnit for MudSelect
        var select = cut.FindComponent<MudSelect<string>>();
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync("pt-PT"));

        // Assert
        _ = await Assert.That(selectedValue).IsEqualTo("pt-PT");
    }
}

public abstract class BunitTestContext : IDisposable
{
    protected Bunit.TestContext Context { get; } = new();

    public BunitTestContext()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Context.JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        _ = Context.JSInterop.SetupVoid("mudPopover.dispose", _ => true);
        _ = Context.JSInterop.Setup<int>("mudpopoverHelper.countProviders").SetResult(1);
        _ = Context.JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
        _ = Context.JSInterop.SetupVoid("mudElementRef.removeOnBlurEvent", _ => true);
        _ = Context.JSInterop.SetupVoid("mudElementRef.focus", _ => true);
        _ = Context.JSInterop.SetupModule("mudAutocomplete");
        _ = Context.JSInterop.SetupModule("mudSelect");
        _ = Context.JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        _ = Context.JSInterop.SetupVoid("mudKeyInterceptor.disconnect", _ => true);
    }

    public void Dispose()
    {
        Context.Dispose();
        GC.SuppressFinalize(this);
    }

    protected IRenderedComponent<TComponent> RenderComponent<TComponent>(
        Action<ComponentParameterCollectionBuilder<TComponent>>? parameterBuilder = null)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        _ = Context.RenderComponent<MudPopoverProvider>();
        return Context.RenderComponent(parameterBuilder);
    }
}
