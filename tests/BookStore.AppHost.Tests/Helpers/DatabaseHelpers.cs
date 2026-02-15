using Aspire.Hosting;
using BookStore.ApiService.Infrastructure.Tenant;
using JasperFx;
using Marten;
using Weasel.Core;

namespace BookStore.AppHost.Tests.Helpers;

public static class DatabaseHelpers
{
    public static async Task SeedTenantAsync(Marten.IDocumentStore store, string tenantId)
    {
        // 1. Ensure Tenant document exists in Marten's native default bucket (for validation)
        await using (var tenantSession = store.LightweightSession())
        {
            var existingTenant = await tenantSession.LoadAsync<BookStore.ApiService.Models.Tenant>(tenantId);
            if (existingTenant == null)
            {
                tenantSession.Store(new BookStore.ApiService.Models.Tenant
                {
                    Id = tenantId,
                    Name = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                        ? "BookStore"
                        : (char.ToUpper(tenantId[0]) + tenantId[1..] + " Corp"),
                    IsEnabled = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await tenantSession.SaveChangesAsync();
            }
        }

        // 2. Seed Admin User in the tenant's own bucket
        await using var session = store.LightweightSession(tenantId);

        var adminEmail = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? "admin@bookstore.com"
            : $"admin@{tenantId}.com";

        // We still use manual store here as TestHelpers might be used in light setup contexts
        // but we fix the normalization mismatch
        var existingUser = await session.Query<BookStore.ApiService.Models.ApplicationUser>()
            .Where(u => u.Email == adminEmail)
            .FirstOrDefaultAsync();

        if (existingUser == null)
        {
            var adminUser = new BookStore.ApiService.Models.ApplicationUser
            {
                UserName = adminEmail,
                NormalizedUserName = adminEmail.ToUpperInvariant(),
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                Roles = ["Admin"],
                SecurityStamp = Guid.CreateVersion7().ToString("D"),
                ConcurrencyStamp = Guid.CreateVersion7().ToString("D")
            };

            var hasher =
                new Microsoft.AspNetCore.Identity.PasswordHasher<BookStore.ApiService.Models.ApplicationUser>();
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

            session.Store(adminUser);
            await session.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets a configured IDocumentStore instance for direct database access in tests.
    /// </summary>
    /// <returns>A configured IDocumentStore with multi-tenancy support.</returns>
    public static async Task<IDocumentStore> GetDocumentStoreAsync()
    {
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        return DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });
    }

    /// <summary>
    /// Gets a user by email from the database.
    /// </summary>
    /// <param name="session">The Marten session to query from.</param>
    /// <param name="email">The user's email address.</param>
    /// <returns>The ApplicationUser if found, null otherwise.</returns>
    public static async Task<BookStore.ApiService.Models.ApplicationUser?> GetUserByEmailAsync(
        IDocumentSession session,
        string email)
    {
        var normalizedEmail = email.ToUpperInvariant();
        return await session.Query<BookStore.ApiService.Models.ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();
    }
}
