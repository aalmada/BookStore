using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Refit;
using TUnit;

namespace BookStore.AppHost.Tests;

public class BookSoftDeleteTests
{
    static string _nonDefaultTenantId = string.Empty;
    static string _nonDefaultAdminEmail = string.Empty;
    static string _nonDefaultAdminPassword = string.Empty;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _nonDefaultTenantId = $"soft-delete-{Guid.CreateVersion7():N}";
        _nonDefaultAdminEmail = $"admin@{_nonDefaultTenantId}.com";
        _nonDefaultAdminPassword = FakeDataGenerators.GenerateFakePassword();

        var tenantsClient = RestService.For<ITenantsClient>(
            HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        try
        {
            await tenantsClient.CreateTenantAsync(new CreateTenantCommand(
                Id: _nonDefaultTenantId,
                Name: "Soft Delete Test Tenant",
                Tagline: null,
                ThemePrimaryColor: null,
                IsEnabled: true,
                AdminEmail: _nonDefaultAdminEmail,
                AdminPassword: _nonDefaultAdminPassword));
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Conflict)
        {
            // Idempotent setup for reruns.
        }
    }

    [Test]
    public async Task SoftDeleteFlow_FullLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var adminClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        // We also need a raw client to fetch ETag, as Refit IBooksClient returns DTOs without headers
        // var rawAdminClient = await HttpClientHelpers.GetAuthenticatedClientAsync();
        var publicClient = Refit.RestService.For<IBooksClient>(HttpClientHelpers.GetUnauthenticatedClient());

        // 1. Create a book
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        // Verify visible in public API
        var initialGet = await publicClient.GetBookAsync(bookId);
        _ = await Assert.That(initialGet).IsNotNull();

        // 2. Soft Delete via Admin API

        // Perform Soft Delete
        var deletedBook = await BookHelpers.DeleteBookAsync(adminClient, createdBook);

        // 3. Verify Public API returns 404
        try
        {
            _ = await publicClient.GetBookAsync(bookId);
            Assert.Fail("Book should have been deleted (404)");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That(ex.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        // 4. Verify Admin API still returns it
        // The Admin Get All endpoint should return it
        var adminGetAll = await adminClient.GetAllBooksAdminAsync();
        var adminBook = adminGetAll!.FirstOrDefault(b => b.Id == bookId);
        _ = await Assert.That(adminBook).IsNotNull();
        _ = await Assert.That(adminBook!.IsDeleted).IsTrue();

        // 5. Restore via Admin API
        createdBook = await BookHelpers.RestoreBookAsync(adminClient, bookId);

        var restoredGet = await publicClient.GetBookAsync(bookId);
        _ = await Assert.That(restoredGet).IsNotNull();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AdminWithIncludeDeletedFalse_ShouldNotSeeDeletedBook(bool sourceIsDefaultTenant)
    {
        var sourceTenant = sourceIsDefaultTenant ? StorageConstants.DefaultTenantId : _nonDefaultTenantId;
        var isolationTenant = sourceIsDefaultTenant ? _nonDefaultTenantId : StorageConstants.DefaultTenantId;

        // Arrange
        var adminClient = await CreateAdminClientForTenantAsync(sourceTenant);
        var isolationAdminClient = await CreateAdminClientForTenantAsync(isolationTenant);
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        _ = await BookHelpers.DeleteBookAsync(adminClient, createdBook);

        // Act + Assert in source tenant
        var finalSearch = await adminClient.GetBooksAsync(new BookSearchRequest
        { Search = createdBook.Title, IncludeDeleted = false });
        _ = await Assert.That(finalSearch!.Items.Any(b => b.Id == bookId)).IsFalse();

        // Explicit tenant isolation assertion
        var isolationSearch = await isolationAdminClient.GetBooksAsync(new BookSearchRequest
        { Search = createdBook.Title, IncludeDeleted = true });
        _ = await Assert.That(isolationSearch!.Items.Any(b => b.Id == bookId)).IsFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AdminWithIncludeDeletedTrue_ShouldSeeDeletedBook_WithIsDeletedTrue(bool sourceIsDefaultTenant)
    {
        var sourceTenant = sourceIsDefaultTenant ? StorageConstants.DefaultTenantId : _nonDefaultTenantId;
        var isolationTenant = sourceIsDefaultTenant ? _nonDefaultTenantId : StorageConstants.DefaultTenantId;

        // Arrange
        var adminClient = await CreateAdminClientForTenantAsync(sourceTenant);
        var isolationAdminClient = await CreateAdminClientForTenantAsync(isolationTenant);
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        _ = await BookHelpers.DeleteBookAsync(adminClient, createdBook);

        // Act + Assert in source tenant
        var finalSearch = await adminClient.GetBooksAsync(new BookSearchRequest
        { Search = createdBook.Title, IncludeDeleted = true });
        var foundBook = finalSearch!.Items.FirstOrDefault(b => b.Id == bookId);
        _ = await Assert.That(foundBook).IsNotNull();
        _ = await Assert.That(foundBook!.IsDeleted).IsTrue();

        // Explicit tenant isolation assertion
        var isolationSearch = await isolationAdminClient.GetBooksAsync(new BookSearchRequest
        { Search = createdBook.Title, IncludeDeleted = true });
        _ = await Assert.That(isolationSearch!.Items.Any(b => b.Id == bookId)).IsFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task NonAdminWithIncludeDeletedTrue_ShouldNotSeeDeletedBook(bool sourceIsDefaultTenant)
    {
        var sourceTenant = sourceIsDefaultTenant ? StorageConstants.DefaultTenantId : _nonDefaultTenantId;
        var isolationTenant = sourceIsDefaultTenant ? _nonDefaultTenantId : StorageConstants.DefaultTenantId;

        // Arrange
        var adminClient = await CreateAdminClientForTenantAsync(sourceTenant);
        var isolationAdminClient = await CreateAdminClientForTenantAsync(isolationTenant);
        var nonAdminClient = await AuthenticationHelpers.CreateUserAndGetClientAsync<IBooksClient>(sourceTenant);
        var createdBook = await BookHelpers.CreateBookAsync(adminClient);
        var bookId = createdBook!.Id;

        _ = await BookHelpers.DeleteBookAsync(adminClient, createdBook);

        // Act + Assert in source tenant
        var finalSearch = await nonAdminClient.GetBooksAsync(new BookSearchRequest
        { Search = createdBook.Title, IncludeDeleted = true });
        _ = await Assert.That(finalSearch!.Items.Any(b => b.Id == bookId)).IsFalse();

        // Explicit tenant isolation assertion
        var isolationSearch = await isolationAdminClient.GetBooksAsync(new BookSearchRequest
        { Search = createdBook.Title, IncludeDeleted = true });
        _ = await Assert.That(isolationSearch!.Items.Any(b => b.Id == bookId)).IsFalse();
    }

    static async Task<IBooksClient> CreateAdminClientForTenantAsync(string tenantId)
    {
        if (StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        }

        LoginResponse? loginResult = null;
        await SseEventHelpers.WaitForConditionAsync(
            async () =>
            {
                var identityClient = RestService.For<IIdentityClient>(
                    HttpClientHelpers.GetUnauthenticatedClient(tenantId));

                try
                {
                    loginResult = await identityClient.LoginAsync(new LoginRequest(
                        _nonDefaultAdminEmail,
                        _nonDefaultAdminPassword));
                    return !string.IsNullOrWhiteSpace(loginResult?.AccessToken);
                }
                catch (ApiException)
                {
                    return false;
                }
            },
            TestConstants.DefaultTimeout,
            $"Failed to login tenant admin for '{tenantId}'.");

        return RestService.For<IBooksClient>(
            HttpClientHelpers.GetAuthenticatedClient(loginResult!.AccessToken, tenantId));
    }
}
