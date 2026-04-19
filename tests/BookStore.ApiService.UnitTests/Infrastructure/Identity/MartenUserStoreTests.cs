using BookStore.ApiService.Infrastructure.Identity;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using Marten;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Infrastructure.Identity;

public class MartenUserStoreTests
{
    #region IsTenantIsolationInvariantSatisfied

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithMatchingTenantIds_ShouldReturnTrue()
    {
        // Arrange
        const string sessionTenantId = "acme";
        const string requestTenantId = "acme";

        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied(sessionTenantId, requestTenantId);

        // Assert
        _ = await Assert.That(isSatisfied).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithCaseInsensitiveMatch_ShouldReturnTrue()
    {
        // Arrange
        const string sessionTenantId = "ACME";
        const string requestTenantId = "acme";

        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied(sessionTenantId, requestTenantId);

        // Assert
        _ = await Assert.That(isSatisfied).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithMismatchedTenantIds_ShouldReturnFalse()
    {
        // Arrange
        const string sessionTenantId = "acme";
        const string requestTenantId = "contoso";

        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied(sessionTenantId, requestTenantId);

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithNullSessionTenantId_ShouldReturnFalse()
    {
        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied(null, "acme");

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithNullRequestTenantId_ShouldReturnFalse()
    {
        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied("acme", null);

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithEmptySessionTenantId_ShouldReturnFalse()
    {
        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied("", "acme");

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithWhitespaceSessionTenantId_ShouldReturnFalse()
    {
        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied("  ", "acme");

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task IsTenantIsolationInvariantSatisfied_WithBothNull_ShouldReturnFalse()
    {
        // Act
        var isSatisfied = MartenUserStore.IsTenantIsolationInvariantSatisfied(null, null);

        // Assert
        _ = await Assert.That(isSatisfied).IsFalse();
    }

    #endregion

    #region EnforceTenantIsolation

    [Test]
    [Category("Unit")]
    public async Task EnforceTenantIsolation_WithNullUser_ShouldReturnNull()
    {
        // Arrange
        var (store, _, _) = CreateStore("acme", "acme");

        // Act
        var result = store.EnforceTenantIsolation(null, "TestMethod");

        // Assert
        _ = await Assert.That(result).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task EnforceTenantIsolation_WithMatchingTenants_ShouldReturnUser()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "test@example.com" };
        var (store, _, _) = CreateStore("acme", "acme");

        // Act
        var result = store.EnforceTenantIsolation(user, "FindByNameAsync");

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = await Assert.That(result!.Id).IsEqualTo(user.Id);
    }

    [Test]
    [Category("Unit")]
    public async Task EnforceTenantIsolation_WithMismatchedTenants_ShouldReturnNull()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "test@example.com" };
        var (store, _, _) = CreateStore("acme", "contoso");

        // Act
        var result = store.EnforceTenantIsolation(user, "FindByNameAsync");

        // Assert
        _ = await Assert.That(result).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task EnforceTenantIsolation_WithMismatchedTenants_ShouldLogCritical()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "test@example.com" };
        var (store, _, logger) = CreateStore("acme", "contoso");

        // Act
        _ = store.EnforceTenantIsolation(user, "FindByEmailAsync");

        // Assert — verify a Critical-level log was emitted
        logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    [Category("Unit")]
    public async Task EnforceTenantIsolation_WithNullSessionTenantId_ShouldReturnNull()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "test@example.com" };
        var (store, _, _) = CreateStore(null!, "acme");

        // Act
        var result = store.EnforceTenantIsolation(user, "FindByPasskeyIdAsync");

        // Assert
        _ = await Assert.That(result).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task EnforceTenantIsolation_WithCaseInsensitiveMatch_ShouldReturnUser()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "test@example.com" };
        var (store, _, _) = CreateStore("ACME", "acme");

        // Act
        var result = store.EnforceTenantIsolation(user, "FindByNameAsync");

        // Assert
        _ = await Assert.That(result).IsNotNull();
        _ = await Assert.That(result!.Id).IsEqualTo(user.Id);
    }

    #endregion

    #region Helpers

    static (MartenUserStore Store, IDocumentSession Session, ILogger<MartenUserStore> Logger) CreateStore(
        string sessionTenantId,
        string requestTenantId)
    {
        var session = Substitute.For<IDocumentSession>();
        _ = session.TenantId.Returns(sessionTenantId);

        var tenantContext = Substitute.For<ITenantContext>();
        _ = tenantContext.TenantId.Returns(requestTenantId);

        var logger = Substitute.For<ILogger<MartenUserStore>>();

        var store = new MartenUserStore(session, tenantContext, logger);
        return (store, session, logger);
    }

    #endregion
}
