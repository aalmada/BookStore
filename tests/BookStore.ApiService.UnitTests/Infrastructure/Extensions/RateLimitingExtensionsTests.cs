using System.Collections;
using System.Reflection;
using BookStore.ApiService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Infrastructure.Extensions;

public class RateLimitingExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_ShouldRegisterNotificationSsePolicy()
    {
        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Development");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();

        // Act
        _ = services.AddCustomRateLimiting(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var hasPolicy = HasPolicy(options, RateLimitingExtensions.NotificationSsePolicyName);

        // Assert
        _ = await Assert.That(hasPolicy).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_WithDisabledFlagInDevelopment_ShouldNotThrow()
    {
        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Development");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Disabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert – must not throw
        _ = await Assert.That(() => _ = services.AddCustomRateLimiting(configuration, environment)).ThrowsNothing();
    }

    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_WithDisabledFlagInProduction_ShouldNotThrow_ButLogs()
    {
        // The guard logs a Critical warning rather than throwing, so the call still succeeds.
        // This test verifies the method completes without exception in production.

        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Production");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Disabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert – must not throw (log-only guard)
        _ = await Assert.That(() => _ = services.AddCustomRateLimiting(configuration, environment)).ThrowsNothing();
    }

    [Test]
    [Category("Unit")]
    public async Task AddCustomRateLimiting_WithDisabledFlagInTestEnvironment_ShouldNotThrow()
    {
        // Arrange
        var environment = Substitute.For<IWebHostEnvironment>();
        _ = environment.EnvironmentName.Returns("Testing");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Disabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        _ = await Assert.That(() => _ = services.AddCustomRateLimiting(configuration, environment)).ThrowsNothing();
    }

    static bool HasPolicy(RateLimiterOptions options, string policyName) => ContainsPolicyName(options, policyName, depth: 0);

    static bool ContainsPolicyName(object? value, string policyName, int depth)
    {
        if (value is null || depth > 5)
        {
            return false;
        }

        if (value is string stringValue)
        {
            return string.Equals(stringValue, policyName, StringComparison.Ordinal);
        }

        if (value is IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
            {
                if (key is string dictionaryKey && string.Equals(dictionaryKey, policyName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (var dictionaryValue in dictionary.Values)
            {
                if (ContainsPolicyName(dictionaryValue, policyName, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                var key = item.GetType().GetProperty("Key")?.GetValue(item)?.ToString();
                if (string.Equals(key, policyName, StringComparison.Ordinal))
                {
                    return true;
                }

                if (ContainsPolicyName(item, policyName, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        var valueType = value.GetType();
        if (valueType.IsPrimitive || valueType.IsEnum)
        {
            return false;
        }

        if (valueType == typeof(decimal) || valueType == typeof(DateTime) || valueType == typeof(DateTimeOffset) || valueType == typeof(TimeSpan))
        {
            return false;
        }

        var fields = valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (ContainsPolicyName(field.GetValue(value), policyName, depth + 1))
            {
                return true;
            }
        }

        var properties = valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.GetIndexParameters().Length == 0);

        foreach (var property in properties)
        {
            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (ContainsPolicyName(propertyValue, policyName, depth + 1))
            {
                return true;
            }
        }

        return false;
    }
}
