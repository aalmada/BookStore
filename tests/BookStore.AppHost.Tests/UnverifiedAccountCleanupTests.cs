using BookStore.ApiService.Handlers.Maintenance;
using BookStore.ApiService.Infrastructure.Identity;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using JasperFx.Core;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weasel.Core;
using Wolverine;

namespace BookStore.AppHost.Tests;

public class UnverifiedAccountCleanupTests
{
    [Test]
    public async Task CleanupHandler_ShouldDeleteStaleUnverifiedAccounts()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var connectionString = await app.GetConnectionStringAsync("bookstore");

        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
        });

        var staleUnverifiedEmail = $"stale_unverified_{Guid.NewGuid()}@example.com";
        var freshUnverifiedEmail = $"fresh_unverified_{Guid.NewGuid()}@example.com";
        var staleVerifiedEmail = $"stale_verified_{Guid.NewGuid()}@example.com";

        await using (var session = store.LightweightSession())
        {
            // 1. Stale unverified account (created 25 hours ago, expiration is 24h)
            session.Store(new ApplicationUser
            {
                Email = staleUnverifiedEmail,
                UserName = staleUnverifiedEmail,
                EmailConfirmed = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-25)
            });

            // 2. Fresh unverified account (created 1 hour ago)
            session.Store(new ApplicationUser
            {
                Email = freshUnverifiedEmail,
                UserName = freshUnverifiedEmail,
                EmailConfirmed = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            });

            // 3. Stale verified account (created 25 hours ago, but verified)
            session.Store(new ApplicationUser
            {
                Email = staleVerifiedEmail,
                UserName = staleVerifiedEmail,
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-25)
            });

            await session.SaveChangesAsync();
        }

        // Act - Call the handler directly with null bus (testability refactor)
        var options =
            Options.Create(new AccountCleanupOptions { Enabled = true, UnverifiedAccountExpirationHours = 24 });

        await using (var session = store.LightweightSession())
        {
            await AccountCleanupHandlers.Handle(
                new CleanupUnverifiedAccounts(),
                session,
                options,
                null!, // No mock needed, logic works without bus
                NullLogger.Instance,
                CancellationToken.None);
        }

        // Assert
        await using var querySession = store.QuerySession();
        var staleUnverifiedUser = await querySession.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Email == staleUnverifiedEmail);
        var freshUnverifiedUser = await querySession.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Email == freshUnverifiedEmail);
        var staleVerifiedUser = await querySession.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Email == staleVerifiedEmail);

        _ = await Assert.That(staleUnverifiedUser).IsNull();
        _ = await Assert.That(freshUnverifiedUser).IsNotNull();
        _ = await Assert.That(staleVerifiedUser).IsNotNull();
    }
}
