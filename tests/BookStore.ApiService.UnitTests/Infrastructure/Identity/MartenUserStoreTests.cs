using BookStore.ApiService.Infrastructure.Identity;

namespace BookStore.ApiService.UnitTests.Infrastructure.Identity;

public class MartenUserStoreTests
{
    [Test]
    [Category("Unit")]
    public async Task IsPasskeyLookupTenantIsolationInvariantSatisfied_WithMatchingTenantIds_ShouldReturnTrue()
    {
        // Arrange
        const string sessionTenantId = "acme";
        const string requestTenantId = "acme";

        // Act
        var isSatisfied = MartenUserStore.IsPasskeyLookupTenantIsolationInvariantSatisfied(sessionTenantId, requestTenantId);

        // Assert
        _ = await Assert.That(isSatisfied).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsPasskeyLookupTenantIsolationInvariantSatisfied_WithMismatchedTenantIds_ShouldReturnFalse()
    {
        // Arrange
        const string sessionTenantId = "acme";
        const string requestTenantId = "contoso";

        // Act
        var isSatisfied = MartenUserStore.IsPasskeyLookupTenantIsolationInvariantSatisfied(sessionTenantId, requestTenantId);

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsPasskeyLookupTenantIsolationInvariantSatisfied_WithMissingTenantId_ShouldReturnFalse()
    {
        // Arrange
        const string requestTenantId = "acme";

        // Act
        var isSatisfied = MartenUserStore.IsPasskeyLookupTenantIsolationInvariantSatisfied(null, requestTenantId);

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }
}
