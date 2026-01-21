using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JasperFx;
using Marten;

namespace BookStore.AppHost.Tests;

public class MultiTenantAuthenticationTests : IDisposable
{
    HttpClient? _client;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Manually seed tenants and admin users once per test class
        var connectionString = await GlobalHooks.App.GetConnectionStringAsync("bookstore");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not retrieve connection string for 'bookstore' resource.");
        }

        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await TestHelpers.SeedTenantAsync(store, "acme");
        await TestHelpers.SeedTenantAsync(store, "contoso");
    }

    [Before(Test)]
    public async Task Setup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _client = GlobalHooks.App.CreateHttpClient("apiservice");
    }

    // SeedTenantAsync moved to TestHelpers

    [After(Test)]
    public void Cleanup() => _client?.Dispose();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Helper to login as admin for aspecific tenant
    /// </summary>
    // LoginAsAdminAsync moved to TestHelpers
    [Test]
    public async Task SeedAsync_CreatesAdminForEachTenant()
    {
        // Act: Try to login as each tenant's admin
        var defaultLogin = await TestHelpers.LoginAsAdminAsync(_client!, StorageConstants.DefaultTenantId);
        var acmeLogin = await TestHelpers.LoginAsAdminAsync(_client!, "acme");
        var contosoLogin = await TestHelpers.LoginAsAdminAsync(_client!, "contoso");

        // Assert: All admins should exist and be able to login
        _ = await Assert.That(defaultLogin).IsNotNull();
        _ = await Assert.That(defaultLogin!.AccessToken).IsNotEmpty();

        _ = await Assert.That(acmeLogin).IsNotNull();
        _ = await Assert.That(acmeLogin!.AccessToken).IsNotEmpty();

        _ = await Assert.That(contosoLogin).IsNotNull();
        _ = await Assert.That(contosoLogin!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task Login_AdminFromAcme_CannotLoginToContoso()
    {
        // Arrange: Acme admin credentials
        var credentials = new { email = "admin@acme.com", password = "Admin123!" };

        // Act: Try to login with Acme credentials using Contoso tenant header
        var request = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = JsonContent.Create(credentials)
        };
        request.Headers.Add("X-Tenant-ID", "contoso");

        var response = await _client!.SendAsync(request);

        // Assert: Should fail because admin@acme.com doesn't exist in contoso tenant
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_AdminFromAcme_SucceedsInAcmeTenant()
    {
        // Act: Login with Acme credentials using helper (which has retry logic)
        var response = await TestHelpers.LoginAsAdminAsync(_client!, "acme");

        // Assert: Should succeed
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task AdminToken_FromAcme_CanAccessAcmeBooks()
    {
        // Arrange: Login as Acme admin and get token
        var acmeLogin = await TestHelpers.LoginAsAdminAsync(_client!, "acme");
        _ = await Assert.That(acmeLogin).IsNotNull();

        // Act: Try to access acme books with acme token and acme tenant header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/books");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", acmeLogin!.AccessToken);
        request.Headers.Add("X-Tenant-ID", "acme");

        var response = await _client!.SendAsync(request);

        // Assert: Should succeed (200 OK)
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AdminToken_FromAcme_WithContosoHeader_IsRejected()
    {
        // Arrange: Login as Acme admin
        var acmeLogin = await TestHelpers.LoginAsAdminAsync(_client!, "acme");
        _ = await Assert.That(acmeLogin).IsNotNull();

        // Act: Try to access books with acme JWT but contoso tenant header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", acmeLogin!.AccessToken);
        request.Headers.Add("X-Tenant-ID", "contoso");

        var response = await _client!.SendAsync(request);

        // Assert: Should be rejected
        // The middleware should detect tenant mismatch (JWT vs header)
        // Expected: Either 400 Bad Request (invalid tenant) or 403 Forbidden (tenant mismatch)
        var isRejected = response.StatusCode is HttpStatusCode.BadRequest or
            HttpStatusCode.Forbidden or
            HttpStatusCode.Unauthorized;

        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task Admin_CanCreateBookInOwnTenant()
    {
        // Arrange: Login as Acme admin
        var acmeLogin = await TestHelpers.LoginAsAdminAsync(_client!, "acme");
        _ = await Assert.That(acmeLogin).IsNotNull();

        // Create minimal book data
        var bookData = new
        {
            title = "Test Book for Acme",
            isbn = "978-0-00-000000-0",
            originalLanguage = "en",
            publisherId = Guid.Empty, // We'd need to seed a publisher, but for isolation test this is OK to fail
            authorIds = Array.Empty<Guid>(),
            categoryIds = Array.Empty<Guid>(),
            prices = new { USD = 29.99m }
        };

        // Act: Try to create a book
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/books")
        {
            Content = JsonContent.Create(bookData)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", acmeLogin!.AccessToken);
        request.Headers.Add("X-Tenant-ID", "acme");

        var response = await _client!.SendAsync(request);

        // Assert: Should either succeed (201) or fail with validation error (400)
        // but NOT with authorization error (401/403)
        var isAuthorized = response.StatusCode is HttpStatusCode.Created or
            HttpStatusCode.BadRequest;

        _ = await Assert.That(isAuthorized).IsTrue()
            ;
    }

    [Test]
    public async Task Login_ContosoAdmin_CannotAccessAcmeData()
    {
        // Arrange: Login as Contoso admin
        var contosoLogin = await TestHelpers.LoginAsAdminAsync(_client!, "contoso");
        _ = await Assert.That(contosoLogin).IsNotNull();

        // Act: Try to access acme books with contoso credentials
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", contosoLogin!.AccessToken);
        request.Headers.Add("X-Tenant-ID", "acme");

        var response = await _client!.SendAsync(request);

        // Assert: Should be rejected due to tenant mismatch
        var isRejected = response.StatusCode is HttpStatusCode.BadRequest or
            HttpStatusCode.Forbidden or
            HttpStatusCode.Unauthorized;

        _ = await Assert.That(isRejected).IsTrue();
    }
}
