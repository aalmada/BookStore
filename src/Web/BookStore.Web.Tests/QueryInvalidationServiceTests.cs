using BookStore.Shared.Notifications;
using BookStore.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core.Hooks;

namespace BookStore.Web.Tests;

public class QueryInvalidationServiceTests
{
    private QueryInvalidationService _sut = null!;

    [Before(Test)]
    public void Setup()
    {
        _sut = new QueryInvalidationService(NullLogger<QueryInvalidationService>.Instance);
    }

    [Test]
    public async Task ShouldInvalidate_ReturnsTrue_WhenKeyMatches()
    {
        var notification = new BookCreatedNotification(Guid.NewGuid(), Guid.NewGuid(), "Title", DateTimeOffset.UtcNow);
        var keys = new[] { "Books" };

        var result = _sut.ShouldInvalidate(notification, keys);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldInvalidate_ReturnsTrue_WhenEntityKeyMatches()
    {
        var bookId = Guid.NewGuid();
        var notification = new BookUpdatedNotification(Guid.NewGuid(), bookId, "Title", DateTimeOffset.UtcNow);
        var keys = new[] { $"Book:{bookId}" };

        var result = _sut.ShouldInvalidate(notification, keys);

        await Assert.That(result).IsTrue();
    }
    
    [Test]
    public async Task ShouldInvalidate_ReturnsFalse_WhenNoKeyMatches()
    {
        var bookId = Guid.NewGuid();
        var notification = new BookUpdatedNotification(Guid.NewGuid(), bookId, "Title", DateTimeOffset.UtcNow);
        var keys = new[] { "Authors" };

        var result = _sut.ShouldInvalidate(notification, keys);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldInvalidate_UserVerified_MatchesUserKey()
    {
        var userId = Guid.NewGuid();
        var notification = new UserVerifiedNotification(Guid.NewGuid(), userId, "email@example.com", DateTimeOffset.UtcNow);
        var keys = new[] { $"User:{userId}" };

        var result = _sut.ShouldInvalidate(notification, keys);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task AllNotificationTypes_ShouldHaveInvalidationLogic()
    {
        // 1. Find all concrete types implementing IDomainEventNotification
        var notificationTypes = typeof(IDomainEventNotification).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IDomainEventNotification).IsAssignableFrom(t))
            .Where(t => t != typeof(PingNotification)); // Exclude Ping as it shouldn't invalidate anything

        // 2. Scan the QueryInvalidationService code (via reflection/runtime check)
        // Since we can't easily check the switch statement coverage directly without source generators or complex expression tree analysis,
        // we will instantiate each notification and ensure it yields at least ONE key (global "Books" or specific "Book:Id").
        // This ensures we didn't forget to add a case for it.

        foreach (var type in notificationTypes)
        {
            try 
            {
                var instance = CreateInstance(type);
                if (instance is IDomainEventNotification notification)
                {
                    // Access the private GetInvalidationKeys method for testing
                    var method = _sut.GetType().GetMethod("GetInvalidationKeys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method == null) throw new Exception("Method GetInvalidationKeys not found");

                    var keys = (IEnumerable<string>)method.Invoke(_sut, new object[] { notification })!;
                    var keyList = keys.ToList();

                    await Assert.That(keyList).IsNotEmpty().Because($"Notification type {type.Name} should yield at least one invalidation key.");
                }
            }
            catch (Exception ex)
            {
                 // CreateInstance might fail if we can't handle the constructor
                 // This failure itself is a good alert that we have a new complex notification type
                 throw new Exception($"Failed to verify notification type {type.Name}: {ex.Message}", ex);
            }
        }
    }

    private object CreateInstance(Type type)
    {
        // Try to find a constructor
        var ctor = type.GetConstructors().FirstOrDefault();
        if (ctor == null) return Activator.CreateInstance(type)!;

        var parameters = ctor.GetParameters().Select(p => GetDefaultValue(p.ParameterType)).ToArray();
        return ctor.Invoke(parameters);
    }

    private object GetDefaultValue(Type type)
    {
        if (type == typeof(string)) return "test";
        if (type == typeof(Guid)) return Guid.NewGuid();
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (type.IsValueType) return Activator.CreateInstance(type)!;
        return null!;
    }
}
