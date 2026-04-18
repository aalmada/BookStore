using System.Net;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class SecurityHeadersTests
{
    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        await DatabaseHelpers.CreateTenantViaApiAsync("tenant-a");
    }

    [Test]
    [Arguments("default")]
    [Arguments("tenant-a")]
    public async Task GetRequest_ShouldIncludeBaselineSecurityHeaders(string tenantId)
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");
        httpClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/api/books");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        _ = await Assert.That(GetHeaderValue(response, "X-Content-Type-Options")).IsEqualTo("nosniff");
        _ = await Assert.That(GetHeaderValue(response, "X-Frame-Options")).IsEqualTo("DENY");
        _ = await Assert.That(GetHeaderValue(response, "Referrer-Policy")).IsEqualTo("no-referrer");
        _ = await Assert.That(GetHeaderValue(response, "Permissions-Policy"))
            .IsEqualTo("geolocation=(), microphone=(), camera=()");
        _ = await Assert.That(GetHeaderValue(response, "Content-Security-Policy"))
            .IsEqualTo("default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");
    }

    [Test]
    public async Task DevelopmentEnvironment_ShouldNotEmitHstsHeader()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;
        var httpClient = app.CreateHttpClient("apiservice");

        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        // Act
        var response = await httpClient.GetAsync("/api/books");

        // Assert
        _ = await Assert.That(response.Headers.Contains("Strict-Transport-Security")).IsFalse();
    }

    static string? GetHeaderValue(HttpResponseMessage response, string headerName)
        => response.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
}
