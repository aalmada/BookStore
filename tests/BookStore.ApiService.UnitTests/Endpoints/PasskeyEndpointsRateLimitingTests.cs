using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Infrastructure.Email;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using BookStore.ApiService.Services;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Endpoints;

public class PasskeyEndpointsRateLimitingTests
{
    [Test]
    [Arguments("/account/passkeys", "GET")]
    [Arguments("/account/passkeys/{id}", "DELETE")]
    [Category("Unit")]
    public async Task MapPasskeyEndpoints_ManagementRoutes_ShouldUseAuthRateLimitingPolicy(string routePattern, string httpMethod)
    {
        // Arrange
        using var app = CreateApplication();

        // Act
        _ = app.MapPasskeyEndpoints();
        var endpoint = FindEndpoint(app, routePattern, httpMethod);
        var rateLimitMetadata = endpoint.Metadata
            .OfType<EnableRateLimitingAttribute>()
            .FirstOrDefault(x => string.Equals(x.PolicyName, "AuthPolicy", StringComparison.Ordinal));

        // Assert
        _ = await Assert.That(rateLimitMetadata).IsNotNull();
        _ = await Assert.That(rateLimitMetadata!.PolicyName).IsEqualTo("AuthPolicy");
    }

    static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder();

        _ = builder.Services.AddSingleton<UserManager<ApplicationUser>>(_ => null!);
        _ = builder.Services.AddSingleton<SignInManager<ApplicationUser>>(_ => null!);
        _ = builder.Services.AddSingleton<IUserStore<ApplicationUser>>(_ => null!);
        _ = builder.Services.AddSingleton<ITenantContext>(_ => null!);
        _ = builder.Services.AddSingleton<ITenantStore>(_ => null!);
        _ = builder.Services.AddSingleton<JwtTokenService>(_ => null!);
        _ = builder.Services.AddSingleton<HybridCache>(_ => null!);
        _ = builder.Services.AddSingleton<Wolverine.IMessageBus>(_ => null!);
        _ = builder.Services.AddSingleton<IOptions<EmailOptions>>(_ => Options.Create(new EmailOptions()));
        _ = builder.Services.AddSingleton<IDocumentSession>(_ => null!);

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
