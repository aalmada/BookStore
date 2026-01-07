using System.Net;

namespace BookStore.AppHost.Tests;

public class PublicApiTests
{
    [Test]
    public async Task GetBooks_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/api/books");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetAuthors_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/api/authors");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetCategories_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/api/categories");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetPublishers_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/api/publishers");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GetPublishers failed with status {response.StatusCode}: {error}");
        }

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
