using BookStore.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class LoginSmokeTests
{
    [Test]
    public async Task Login_ShouldExposeExpectedRoute()
    {
        var routeAttribute = typeof(Login).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .SingleOrDefault();

        _ = await Assert.That(routeAttribute).IsNotNull();
        _ = await Assert.That(routeAttribute!.Template).IsEqualTo("/login");
    }
}
