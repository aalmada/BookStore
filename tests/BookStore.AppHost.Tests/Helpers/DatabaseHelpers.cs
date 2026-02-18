using Aspire.Hosting;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests.Helpers;

public static class DatabaseHelpers
{
    /// <summary>
    /// Creates a tenant and its admin user via the API (enforcing full tenant isolation).
    /// Uses the default tenant admin token to call POST /api/admin/tenants.
    /// Idempotent: if the tenant already exists the 409 conflict is silently ignored.
    /// </summary>
    public static async Task SeedTenantAsync(Marten.IDocumentStore _, string tenantId)
    {
        // Default tenant is bootstrapped directly in GlobalSetup — skip here.
        if (StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await CreateTenantViaApiAsync(tenantId);
    }

    /// <summary>
    /// Creates a tenant via the admin API, which also seeds the tenant admin user.
    /// The admin email follows the convention admin@{tenantId}.com with password Admin123!.
    /// Idempotent: duplicate-tenant responses (400/409) are silently ignored.
    /// </summary>
    public static async Task CreateTenantViaApiAsync(string tenantId)
    {
        if (GlobalHooks.AdminAccessToken == null)
        {
            throw new InvalidOperationException("AdminAccessToken is not set. Ensure GlobalSetup has completed.");
        }

        var tenantName = char.ToUpper(tenantId[0]) + tenantId[1..];

        var client = RestService.For<ITenantsClient>(HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken));

        try
        {
            await client.CreateTenantAsync(new CreateTenantCommand(
                Id: tenantId,
                Name: tenantName,
                Tagline: null,
                ThemePrimaryColor: null,
                IsEnabled: true,
                AdminEmail: $"admin@{tenantId}.com",
                AdminPassword: "Admin123!"));
        }
        catch (Refit.ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Conflict
                                                            or System.Net.HttpStatusCode.BadRequest)
        {
            // Tenant already exists — idempotent, ignore.
        }
    }

    /// <summary>
    /// Gets a configured IDocumentStore instance for direct database access in tests.
    /// </summary>
    /// <returns>A configured IDocumentStore with multi-tenancy support.</returns>
    /// <remarks>
    /// IMPORTANT: Callers MUST dispose the returned IDocumentStore to prevent connection leaks.
    /// Use the 'await using' pattern:
    /// <code>
    /// await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
    /// await using var session = store.LightweightSession(tenantId);
    /// // ... use session ...
    /// </code>
    /// Each DocumentStore maintains its own connection pool. Failing to dispose will exhaust
    /// PostgreSQL's connection limit during parallel test execution.
    /// </remarks>
    public static async Task<IDocumentStore> GetDocumentStoreAsync()
    {
        var baseConnectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");

        // Configure connection pooling to limit connections per DocumentStore
        // This prevents parallel tests from exhausting PostgreSQL's connection limit
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            MaxPoolSize = 10,  // Reduced from default 100 - each store gets max 10 connections
            MinPoolSize = 0,   // Don't pre-allocate connections
            ConnectionLifetime = 300  // 5 minutes - recycle connections periodically
        };

        return DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(builder.ConnectionString);
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
