using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class RateLimitingExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task BuildAuthPolicyPartitionKey_WithTenantAndIp_ShouldIncludeBothParts()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items["TenantId"] = "tenant-a";
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        // Act
        var key = RateLimitingExtensions.BuildAuthPolicyPartitionKey(context);

        // Assert
        _ = await Assert.That(key).IsEqualTo("tenant-a:203.0.113.10");
    }

    [Test]
    [Category("Unit")]
    public async Task BuildAuthPolicyPartitionKey_WithoutTenantOrIp_ShouldUseSafeDefaults()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var key = RateLimitingExtensions.BuildAuthPolicyPartitionKey(context);

        // Assert
        _ = await Assert.That(key).IsEqualTo($"{JasperFx.StorageConstants.DefaultTenantId}:unknown");
    }

    [Test]
    [Category("Unit")]
    public async Task BuildAuthPolicyPartitionKey_SameTenantAndIp_WithDifferentEmailItems_ShouldSharePartition()
    {
        // Arrange
        var contextWithFirstEmail = new DefaultHttpContext();
        contextWithFirstEmail.Items["TenantId"] = "tenant-a";
        contextWithFirstEmail.Items["AuthRateLimitEmail"] = "first@example.com";
        contextWithFirstEmail.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var contextWithSecondEmail = new DefaultHttpContext();
        contextWithSecondEmail.Items["TenantId"] = "tenant-a";
        contextWithSecondEmail.Items["AuthRateLimitEmail"] = "second@example.com";
        contextWithSecondEmail.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        // Act
        var firstKey = RateLimitingExtensions.BuildAuthPolicyPartitionKey(contextWithFirstEmail);
        var secondKey = RateLimitingExtensions.BuildAuthPolicyPartitionKey(contextWithSecondEmail);

        // Assert
        _ = await Assert.That(firstKey).IsEqualTo("tenant-a:203.0.113.10");
        _ = await Assert.That(secondKey).IsEqualTo(firstKey);
    }

    [Test]
    [Category("Unit")]
    public async Task BuildAuthPolicyPartitionKey_DifferentTenant_WithSameIp_ShouldUseDifferentPartitions()
    {
        // Arrange
        var contextForTenantA = new DefaultHttpContext();
        contextForTenantA.Items["TenantId"] = "tenant-a";
        contextForTenantA.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var contextForTenantB = new DefaultHttpContext();
        contextForTenantB.Items["TenantId"] = "tenant-b";
        contextForTenantB.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        // Act
        var keyForTenantA = RateLimitingExtensions.BuildAuthPolicyPartitionKey(contextForTenantA);
        var keyForTenantB = RateLimitingExtensions.BuildAuthPolicyPartitionKey(contextForTenantB);

        // Assert
        _ = await Assert.That(keyForTenantA).IsEqualTo("tenant-a:203.0.113.10");
        _ = await Assert.That(keyForTenantB).IsEqualTo("tenant-b:203.0.113.10");
        _ = await Assert.That(keyForTenantB).IsNotEqualTo(keyForTenantA);
    }

    [Test]
    [Category("Unit")]
    public async Task AuthPartitioning_SameTenantAndIp_WithDifferentEmails_ShouldShareThrottleBucket()
    {
        // Arrange
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                RateLimitingExtensions.BuildAuthPolicyPartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

        var firstAttempt = new DefaultHttpContext();
        firstAttempt.Items["TenantId"] = "tenant-a";
        firstAttempt.Items["AuthRateLimitEmail"] = "first@example.com";
        firstAttempt.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var secondAttemptDifferentEmail = new DefaultHttpContext();
        secondAttemptDifferentEmail.Items["TenantId"] = "tenant-a";
        secondAttemptDifferentEmail.Items["AuthRateLimitEmail"] = "second@example.com";
        secondAttemptDifferentEmail.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var differentTenant = new DefaultHttpContext();
        differentTenant.Items["TenantId"] = "tenant-b";
        differentTenant.Items["AuthRateLimitEmail"] = "second@example.com";
        differentTenant.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        // Act
        using var firstLease = await limiter.AcquireAsync(firstAttempt, permitCount: 1);
        using var secondLease = await limiter.AcquireAsync(secondAttemptDifferentEmail, permitCount: 1);
        using var differentTenantLease = await limiter.AcquireAsync(differentTenant, permitCount: 1);

        // Assert
        _ = await Assert.That(firstLease.IsAcquired).IsTrue();
        _ = await Assert.That(secondLease.IsAcquired).IsFalse();
        _ = await Assert.That(differentTenantLease.IsAcquired).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task WriteRateLimitProblemDetailsAsync_ShouldWriteRfc7807Payload()
    {
        // Arrange
        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Act
        await RateLimitingExtensions.WriteRateLimitProblemDetailsAsync(context, 12.5, CancellationToken.None);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        // Assert
        _ = await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status429TooManyRequests);
        _ = await Assert.That(context.Response.ContentType).IsEqualTo("application/problem+json");

        var root = document.RootElement;
        _ = await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status429TooManyRequests);
        _ = await Assert.That(root.GetProperty("title").GetString()).IsEqualTo("Too Many Requests");
        _ = await Assert.That(root.GetProperty("error").GetString()).IsEqualTo(ErrorCodes.Auth.RateLimitExceeded);
        _ = await Assert.That(root.GetProperty("retryAfter").GetDouble()).IsEqualTo(12.5);
    }
}
