using Bogus;
using BookStore.Web.Components.Shared;
using BookStore.Web.Tests.Infrastructure;
using Bunit;

namespace BookStore.Web.Tests.Components;

public class AlertBannerTests : BunitTestContext
{
    [Test]
    public async Task AlertBanner_ShouldRenderMessageWithSeverityClass()
    {
        var faker = new Faker();
        var message = faker.Lorem.Sentence(6);

        var cut = RenderComponent<AlertBanner>(parameters => parameters
            .Add(p => p.Message, message)
            .Add(p => p.Severity, AlertBanner.AlertSeverity.Warning));

        var banner = cut.Find("div[role='alert']");

        _ = await Assert.That(banner.TextContent).Contains(message);
        _ = await Assert.That(banner.ClassList.Contains("alert-warning")).IsTrue();
    }
}