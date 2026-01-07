using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Projects;

namespace BookStore.AppHost.Tests;

public class FrontendTests
{
    [Test]
    public async Task GetWebFrontendHealthCallbackReturnsOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        var httpClient = app.CreateHttpClient("webfrontend");

        await notificationService.WaitForResourceHealthyAsync("webfrontend", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/health", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
