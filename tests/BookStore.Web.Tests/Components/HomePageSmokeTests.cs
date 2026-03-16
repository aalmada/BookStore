using BookStore.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class HomePageSmokeTests
{
    [Test]
    public async Task HomePage_ShouldExposeExpectedRoute()
    {
        var routeAttribute = typeof(Home).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .SingleOrDefault();

        _ = await Assert.That(routeAttribute).IsNotNull();
        _ = await Assert.That(routeAttribute!.Template).IsEqualTo("/");
    }
}
