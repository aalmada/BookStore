using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class MultiTenantAuthenticationTests : IDisposable
{
    static string _tenant1 = string.Empty;
    static string _tenant2 = string.Empty;

    HttpClient? _client;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        _tenant2 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(_tenant1);
        await DatabaseHelpers.CreateTenantViaApiAsync(_tenant2);
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
    public async Task TenantCreation_CreatesAdminForEachTenant()
    {
        // Assert: All tenant admins (created via API during ClassSetup) can log in
        var defaultLogin = await AuthenticationHelpers.LoginAsAdminAsync(_client!, MultiTenancyConstants.DefaultTenantId);
        var tenant1Login = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant1);
        var tenant2Login = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant2);

        _ = await Assert.That(defaultLogin).IsNotNull();
        _ = await Assert.That(defaultLogin!.AccessToken).IsNotEmpty();

        _ = await Assert.That(tenant1Login).IsNotNull();
        _ = await Assert.That(tenant1Login!.AccessToken).IsNotEmpty();

        _ = await Assert.That(tenant2Login).IsNotNull();
        _ = await Assert.That(tenant2Login!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task Login_AdminFromTenant1_CannotLoginToTenant2()
    {
        // Arrange: tenant1 admin credentials
        var credentials = new { email = $"admin@{_tenant1}.com", password = "Admin123!" };

        // Act: Try to login with tenant1 credentials using tenant2 header
        var request = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = JsonContent.Create(credentials)
        };
        request.Headers.Add("X-Tenant-ID", _tenant2);

        var response = await _client!.SendAsync(request);

        // Assert: Should fail because admin@{_tenant1}.com doesn't exist in tenant2
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_AdminFromTenant1_SucceedsInTenant1()
    {
        // Act: Login with tenant1 credentials
        var response = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant1);

        // Assert: Should succeed
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task AdminToken_FromTenant1_CanAccessTenant1Books()
    {
        // Arrange: Login as tenant1 admin and get token
        var tenant1Login = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant1);
        _ = await Assert.That(tenant1Login).IsNotNull();

        // Act: Access books with matching token and tenant header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/books");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tenant1Login!.AccessToken);
        request.Headers.Add("X-Tenant-ID", _tenant1);

        var response = await _client!.SendAsync(request);

        // Assert: Should succeed (200 OK)
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AdminToken_FromTenant1_WithTenant2Header_IsRejected()
    {
        // Arrange: Login as tenant1 admin
        var tenant1Login = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant1);
        _ = await Assert.That(tenant1Login).IsNotNull();

        // Act: Use tenant1 JWT with tenant2 header (cross-tenant attack)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tenant1Login!.AccessToken);
        request.Headers.Add("X-Tenant-ID", _tenant2);

        var response = await _client!.SendAsync(request);

        // Assert: Middleware detects JWT/header tenant mismatch
        var isRejected = response.StatusCode is HttpStatusCode.BadRequest or
            HttpStatusCode.Forbidden or
            HttpStatusCode.Unauthorized;

        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task Admin_CanCreateBookInOwnTenant()
    {
        // Arrange: Login as tenant1 admin
        var tenant1Login = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant1);
        _ = await Assert.That(tenant1Login).IsNotNull();

        // Build tenant1-scoped HTTP client
        var tenantHttpClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        tenantHttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenant1Login!.AccessToken);
        tenantHttpClient.DefaultRequestHeaders.Add("X-Tenant-ID", _tenant1);

        // Create tenant1-scoped Refit clients
        var publishersClient = RestService.For<IPublishersClient>(tenantHttpClient);
        var authorsClient = RestService.For<IAuthorsClient>(tenantHttpClient);
        var categoriesClient = RestService.For<ICategoriesClient>(tenantHttpClient);
        var booksClient = RestService.For<IBooksClient>(tenantHttpClient);

        // Create required dependencies within tenant1
        var publisher = await PublisherHelpers.CreatePublisherAsync(
            publishersClient, FakeDataGenerators.GenerateFakePublisherRequest());
        var author = await AuthorHelpers.CreateAuthorAsync(
            authorsClient, FakeDataGenerators.GenerateFakeAuthorRequest());
        var category = await CategoryHelpers.CreateCategoryAsync(
            categoriesClient, FakeDataGenerators.GenerateFakeCategoryRequest());

        var createBookRequest = FakeDataGenerators.GenerateFakeBookRequest(
            publisher.Id, [author.Id], [category.Id]);

        // Act - Create a book inside tenant1
        var book = await BookHelpers.CreateBookAsync(booksClient, createBookRequest);

        // Assert - Book was created and belongs to tenant1
        _ = await Assert.That(book).IsNotNull();
        _ = await Assert.That(book.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task Login_Tenant2Admin_CannotAccessTenant1Data()
    {
        // Arrange: Login as tenant2 admin
        var tenant2Login = await AuthenticationHelpers.LoginAsAdminAsync(_client!, _tenant2);
        _ = await Assert.That(tenant2Login).IsNotNull();

        // Act: Send tenant2 JWT with tenant1 header (cross-tenant attack)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tenant2Login!.AccessToken);
        request.Headers.Add("X-Tenant-ID", _tenant1);

        var response = await _client!.SendAsync(request);

        // Assert: Should be rejected due to tenant mismatch
        var isRejected = response.StatusCode is HttpStatusCode.BadRequest or
            HttpStatusCode.Forbidden or
            HttpStatusCode.Unauthorized;

        _ = await Assert.That(isRejected).IsTrue();
    }
}
