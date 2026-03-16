using Bogus;
using BookStore.Web.Components.Shared;
using BookStore.Web.Tests.Infrastructure;
using Bunit;

namespace BookStore.Web.Tests.Components;

public class EmptyStateTests : BunitTestContext
{
    [Test]
    public async Task EmptyState_ShouldRenderTitleAndMessage()
    {
        var faker = new Faker();
        var title = faker.Lorem.Sentence(3);
        var message = faker.Lorem.Sentence(8);

        var cut = RenderComponent<EmptyState>(parameters => parameters
            .Add(p => p.Title, title)
            .Add(p => p.Message, message));

        _ = await Assert.That(cut.Find("h2").TextContent).IsEqualTo(title);
        _ = await Assert.That(cut.Find("p").TextContent).IsEqualTo(message);
    }
}