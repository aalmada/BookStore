using System.Net;
using System.Net.ServerSentEvents;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using Refit;

namespace BookStore.AppHost.Tests;

public class PublicApiTests
{
    [Test]
    public async Task GetBooks_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var client = RestService.For<IBooksClient>(httpClient);

        // Act
        // GetBooksAsync requires page and pageSize (nullable)
        var response = await client.GetBooksAsync(new BookStore.Shared.Models.BookSearchRequest());

        // Assert
        _ = await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task GetAuthors_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var client = RestService.For<IAuthorsClient>(httpClient);

        // Act
        // Public API does not support search request object, only page/pageSize
        var response = await client.GetAuthorsAsync(null, null);

        // Assert
        _ = await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task GetCategories_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var client = RestService.For<ICategoriesClient>(httpClient);

        // Act
        // GetCategoriesAsync takes page, pageSize, sortBy, sortOrder
        var response = await client.GetCategoriesAsync(null, null, null, null);

        // Assert
        _ = await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task GetPublishers_PublicEndpoint_ShouldReturnOk()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        var client = RestService.For<IPublishersClient>(httpClient);

        // Act
        var response = await client.GetPublishersAsync(null, null);

        // Assert
        _ = await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task GetNotificationStream_AnonymousAccess_ShouldReturnSseStream()
    {
        // The stream is intentionally accessible to anonymous users to support real-time
        // updates on the public catalog. This test verifies that anonymous access works.
        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        httpClient.Timeout = TestConstants.DefaultStreamTimeout;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await httpClient.GetAsync(
            "/api/notifications/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        _ = await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("text/event-stream");
    }
}
