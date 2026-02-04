using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace BookStore.Web.Tests.Infrastructure;

public abstract class BunitTestContext : IDisposable
{
    protected Bunit.TestContext Context { get; } = new();

    protected BunitTestContext()
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
