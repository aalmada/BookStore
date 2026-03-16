using BookStore.Client;
using BookStore.Client.Services;
using BookStore.Web.Components.Layout;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class NavMenuSmokeTests : BunitTestContext
{
    [Before(Test)]
    public void Setup()
    {
        _ = Context.Services.AddLogging();

        var cartClient = Substitute.For<IShoppingCartClient>();
        cartClient.GetShoppingCartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ShoppingCartResponse([], 0)));

        var eventsService = new BookStoreEventsService(
            new HttpClient { BaseAddress = new Uri("http://localhost") },
            Substitute.For<ILogger<BookStoreEventsService>>(),
            new ClientContextService());

        _ = Context.Services.AddSingleton(cartClient);
        _ = Context.Services.AddSingleton(eventsService);
        _ = Context.Services.AddSingleton(new QueryInvalidationService(Substitute.For<ILogger<QueryInvalidationService>>()));

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
