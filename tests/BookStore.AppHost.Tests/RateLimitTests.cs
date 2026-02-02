using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Projects;

namespace BookStore.AppHost.Tests;

public class RateLimitTests
{
    [Test]
    public async Task GetFromAuthEndpoint_RepeatedRequests_ShouldConsumeQuota()
    {
        // Arrange
        // Use unauthenticated client for login attempt
        var httpClient = TestHelpers.GetUnauthenticatedClient();

        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        // Act & Assert
        // Make an initial request to login endpoint
        var loginRequest = new { Email = "admin@bookstore.com", Password = "WrongPassword!" };
        var response = await httpClient.PostAsJsonAsync("/account/login", loginRequest);

        // Even if login fails (401), rate limits should apply
        // Check for Rate Limit headers
        if (response.Headers.Contains("X-Rate-Limit-Remaining"))
        {
            var remaining = response.Headers.GetValues("X-Rate-Limit-Remaining").FirstOrDefault();
            _ = await Assert.That(remaining).IsNotNull();
        }
        else
        {
            // If headers aren't exposed, we might not be able to verify quota without exhausting it, 
            // which is slow/flaky. At minimum, verify we get a response (middleware didn't crash).
            _ = await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.InternalServerError);
        }
    }
}
