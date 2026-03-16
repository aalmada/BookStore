using BookStore.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using TUnit.Core;

namespace BookStore.Web.Tests.Components;

public class BookDetailsSmokeTests
{
    [Test]
    public async Task BookDetails_ShouldExposeExpectedRoute()
    {
        var routeAttribute = typeof(BookDetails).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .SingleOrDefault();

        _ = await Assert.That(routeAttribute).IsNotNull();
        _ = await Assert.That(routeAttribute!.Template).IsEqualTo("/book/{id:guid}");
    }
}
