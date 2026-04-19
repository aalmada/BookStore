using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace BookStore.ApiService.UnitTests.Endpoints;

public class NotificationEndpointsRateLimitingTests
{
    [Test]
    [Category("Unit")]
    public async Task MapNotificationEndpoints_StreamRoute_ShouldUseNotificationSseRateLimitingPolicy()
    {
        // Arrange
        using var app = CreateApplication();

        // Act
        _ = app.MapGroup("/api/notifications").MapNotificationEndpoints();
        var endpoint = FindEndpoint(app, "/api/notifications/stream", "GET");
        var rateLimitMetadata = endpoint.Metadata
            .OfType<EnableRateLimitingAttribute>()
            .FirstOrDefault(x => string.Equals(x.PolicyName, RateLimitingExtensions.NotificationSsePolicyName, StringComparison.Ordinal));

        // Assert
        _ = await Assert.That(rateLimitMetadata).IsNotNull();
        _ = await Assert.That(rateLimitMetadata!.PolicyName).IsEqualTo(RateLimitingExtensions.NotificationSsePolicyName);
    }

    [Test]
    [Category("Unit")]
    public async Task MapNotificationEndpoints_StreamRoute_ShouldRemainAnonymous()
    {
        // Arrange
        using var app = CreateApplication();

        // Act
        _ = app.MapGroup("/api/notifications").MapNotificationEndpoints();
        var endpoint = FindEndpoint(app, "/api/notifications/stream", "GET");
        var allowAnonymousMetadata = endpoint.Metadata.GetMetadata<IAllowAnonymous>();

        // Assert
        _ = await Assert.That(allowAnonymousMetadata).IsNotNull();
    }

    [Test]
    [Category("Unit")]
    public async Task MapNotificationEndpoints_TestNotificationRoute_ShouldRequireAdminAuthorization()
    {
        // Arrange
        using var app = CreateApplication();

        // Act
        _ = app.MapGroup("/api/notifications").MapNotificationEndpoints();
        var endpoint = FindEndpoint(app, "/api/notifications/test-notification", "POST");
        var authorizeMetadata = endpoint.Metadata.OfType<IAuthorizeData>().ToArray();
        var adminPolicyMetadata = authorizeMetadata.FirstOrDefault(x => string.Equals(x.Policy, "Admin", StringComparison.Ordinal));
        var allowAnonymousMetadata = endpoint.Metadata.GetMetadata<IAllowAnonymous>();

        // Assert
        _ = await Assert.That(adminPolicyMetadata).IsNotNull();
        _ = await Assert.That(allowAnonymousMetadata is null).IsTrue();
    }

    static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.Services.AddLogging();
        _ = builder.Services.AddSingleton<INotificationService>(_ => null!);
        return builder.Build();
    }

    static RouteEndpoint FindEndpoint(WebApplication app, string routePattern, string httpMethod)
    {
        var endpointRouteBuilder = (IEndpointRouteBuilder)app;

        return endpointRouteBuilder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => MatchesEndpoint(endpoint, routePattern, httpMethod));
    }

    static bool MatchesEndpoint(RouteEndpoint endpoint, string routePattern, string httpMethod)
    {
        if (!string.Equals(endpoint.RoutePattern.RawText, routePattern, StringComparison.Ordinal))
        {
            return false;
        }

        var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        if (httpMethods is null)
        {
            return false;
        }

        return httpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase);
    }
}
