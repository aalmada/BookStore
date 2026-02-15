using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BookStore.ServiceDefaults;
using Projects;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class FrontendTests
{
    [Test]
    public async Task GetWebFrontendHealthCallbackReturnsOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        var httpClient = app.CreateHttpClient(ResourceNames.WebFrontend);

        _ = await notificationService.WaitForResourceHealthyAsync(ResourceNames.WebFrontend, CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync(ResourceNames.HealthCheckEndpoint, CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetWebFrontendRoot_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        var httpClient = app.CreateHttpClient(ResourceNames.WebFrontend);

        _ = await notificationService.WaitForResourceHealthyAsync(ResourceNames.WebFrontend, CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
