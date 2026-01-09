using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Projects;

namespace BookStore.AppHost.Tests;

public class CorsTests
{
    [Test]
    public async Task OptionsRequest_WithAllowedOrigin_ShouldReturnCorsHeaders()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/books");
        request.Headers.Add("Origin", "https://localhost:7260"); // Matches allowed origin in Program.cs
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await httpClient.SendAsync(request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent).Or.IsEqualTo(HttpStatusCode.OK);

        var allowOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        _ = await Assert.That(allowOrigin).IsEqualTo("https://localhost:7260");
    }

    [Test]
    public async Task OptionsRequest_WithDisallowedOrigin_ShouldNotReturnCorsHeaders()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/books");
        request.Headers.Add("Origin", "http://evil.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await httpClient.SendAsync(request);

        // Assert
        // Default behavior for disallowed origin is usually to NOT send CORS headers.
        // The status code might still be 2xx (successful OPTIONS), but without the A-C-A-O header.

        var hasAllowOrigin = response.Headers.TryGetValues("Access-Control-Allow-Origin", out _);
        _ = await Assert.That(hasAllowOrigin).IsFalse();
    }
}
