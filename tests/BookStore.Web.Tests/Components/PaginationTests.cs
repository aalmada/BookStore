using BookStore.Web.Components.Shared;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BookStore.Web.Tests.Components;

public class PaginationTests : BunitTestContext
{
    [Test]
    public async Task Pagination_ShouldRenderNavigationControlsAndInvokeCallbacks()
    {
        var previousClicked = false;
        var nextClicked = false;

        var cut = RenderComponent<Pagination>(parameters => parameters
            .Add(p => p.PageNumber, 2)
            .Add(p => p.PageCount, 5)
            .Add(p => p.HasPreviousPage, true)
            .Add(p => p.HasNextPage, true)
            .Add(p => p.OnPrev, EventCallback.Factory.Create(this, () => previousClicked = true))
            .Add(p => p.OnNext, EventCallback.Factory.Create(this, () => nextClicked = true)));

        var buttons = cut.FindAll("button");
        cut.Find("button:first-child").Click();
        cut.Find("button:last-child").Click();

        _ = await Assert.That(buttons.Count).IsEqualTo(2);
        _ = await Assert.That(cut.Markup).Contains("Page 2 of 5");
        _ = await Assert.That(previousClicked).IsTrue();
        _ = await Assert.That(nextClicked).IsTrue();
    }
}
