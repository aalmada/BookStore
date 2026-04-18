using System.Net;
using System.Text.Json;
using BookStore.ApiService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class RateLimitingExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task BuildAuthPolicyPartitionKey_WithEmailTenantAndIp_ShouldNormalizeAndIncludeAllParts()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items["TenantId"] = "tenant-a";
        context.Items[RateLimitingExtensions.AuthRateLimitEmailItemKey] = "  User@Test.com  ";
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        // Act
        var key = RateLimitingExtensions.BuildAuthPolicyPartitionKey(context);

        // Assert
        _ = await Assert.That(key).IsEqualTo("tenant-a:203.0.113.10:USER@TEST.COM");
    }

    [Test]
    [Category("Unit")]
    public async Task BuildAuthPolicyPartitionKey_WithoutTenantIpOrEmail_ShouldUseSafeDefaults()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var key = RateLimitingExtensions.BuildAuthPolicyPartitionKey(context);

        // Assert
        _ = await Assert.That(key).IsEqualTo($"{JasperFx.StorageConstants.DefaultTenantId}:unknown:anonymous");
    }

    [Test]
    [Category("Unit")]
    public async Task TryGetAuthEmail_WithValidEmailProperty_ShouldReturnTrueAndTrimmedEmail()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {
              "email": "  person@example.com  "
            }
            """);

        // Act
        var success = RateLimitingExtensions.TryGetAuthEmail(document.RootElement, out var email);

        // Assert
        _ = await Assert.That(success).IsTrue();
        _ = await Assert.That(email).IsEqualTo("person@example.com");
    }

    [Test]
    [Category("Unit")]
    [Arguments("{}")]
    [Arguments("{\"email\":\"\"}")]
    [Arguments("{\"email\":\"   \"}")]
    public async Task TryGetAuthEmail_WithMissingOrBlankEmail_ShouldReturnFalse(string json)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);

        // Act
        var success = RateLimitingExtensions.TryGetAuthEmail(document.RootElement, out var email);

        // Assert
        _ = await Assert.That(success).IsFalse();
        _ = await Assert.That(email).IsEqualTo(string.Empty);
    }
}
