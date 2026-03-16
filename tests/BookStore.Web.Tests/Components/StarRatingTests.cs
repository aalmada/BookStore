using BookStore.Web.Components.Catalog;
using BookStore.Web.Tests.Infrastructure;
using Bunit;

namespace BookStore.Web.Tests.Components;

public class StarRatingTests : BunitTestContext
{
    [Test]
    public async Task StarRating_ShouldRenderReadonlyStarsForCurrentValue()
    {
        var cut = RenderComponent<StarRating>(parameters => parameters
            .Add(p => p.Value, 3)
            .Add(p => p.Editable, false));

        var starButtons = cut.FindAll("button.star-button");
        var activeStars = starButtons.Count(button => button.ClassList.Contains("is-active"));

        _ = await Assert.That(starButtons.Count).IsEqualTo(5);
        _ = await Assert.That(activeStars).IsEqualTo(3);
        _ = await Assert.That(starButtons.All(button => button.HasAttribute("disabled"))).IsTrue();
    }
}