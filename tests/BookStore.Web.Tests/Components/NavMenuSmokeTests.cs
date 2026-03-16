using BookStore.Web.Components.Layout;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class NavMenuSmokeTests : BunitTestContext
{
    [Before(Test)]
    public void Setup()
    {
        var authContext = Context.AddTestAuthorization();
        _ = authContext.SetAuthorized("test-user");
        _ = authContext.SetPolicies("SystemAdmin");
    }

    [Test]
    public async Task NavMenu_ShouldRenderAtLeastOneLink()
    {
        var cut = Context.RenderComponent<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<NavMenu>());

        _ = await Assert.That(cut.FindAll("a").Count).IsGreaterThan(0);
    }
}
