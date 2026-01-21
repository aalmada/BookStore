using System.Net;
using System.Net.Http.Json;
using JasperFx;
using Marten;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Tests to verify that user accounts are properly isolated between tenants.
/// CRITICAL: These tests expose a security vulnerability where users can authenticate across tenants.
/// </summary>
public class AccountIsolationTests : IDisposable
{
    HttpClient? _client;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Ensure tenants exist
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
    public void Setup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _client = GlobalHooks.App.CreateHttpClient("apiservice");
    }

    [After(Test)]
    public void Cleanup() => _client?.Dispose();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task User_RegisteredOnContoso_CannotLoginOnAcme()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        // Act 1: Register user on Contoso tenant
        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/account/register")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        registerRequest.Headers.Add("X-Tenant-ID", "contoso");

        var registerResponse = await _client!.SendAsync(registerRequest);

        // Assert: Registration should succeed
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act 2: Attempt to login with the same credentials on Acme tenant
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        loginRequest.Headers.Add("X-Tenant-ID", "acme");

        var loginResponse = await _client!.SendAsync(loginRequest);

        // Assert: Login should FAIL because user is registered on Contoso, not Acme
        // EXPECTED: 401 Unauthorized
        // CURRENT (BUG): May return 200 OK, which is the security vulnerability
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized)
            ;
    }

    [Test]
    public async Task User_RegisteredOnContoso_CanLoginOnContoso()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        // Act 1: Register user on Contoso tenant
        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/account/register")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        registerRequest.Headers.Add("X-Tenant-ID", "contoso");

        var registerResponse = await _client!.SendAsync(registerRequest);

        // Assert: Registration should succeed
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act 2: Login with the same credentials on Contoso tenant (correct tenant)
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        loginRequest.Headers.Add("X-Tenant-ID", "contoso");

        var loginResponse = await _client!.SendAsync(loginRequest);

        // Assert: Login should succeed on the correct tenant
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task User_RegisteredOnAcme_CannotLoginOnContoso()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        // Act 1: Register user on Acme tenant
        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/account/register")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        registerRequest.Headers.Add("X-Tenant-ID", "acme");

        var registerResponse = await _client!.SendAsync(registerRequest);

        // Assert: Registration should succeed
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act 2: Attempt to login with the same credentials on Contoso tenant
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        loginRequest.Headers.Add("X-Tenant-ID", "contoso");

        var loginResponse = await _client!.SendAsync(loginRequest);

        // Assert: Login should FAIL because user is registered on Acme, not Contoso
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized)
            ;
    }

    [Test]
    public async Task User_RegisteredOnDefault_CannotLoginOnAcme()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        // Act 1: Register user on Default tenant (no X-Tenant-ID header)
        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/account/register")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        // No X-Tenant-ID header = defaults to StorageConstants.DefaultTenantId

        var registerResponse = await _client!.SendAsync(registerRequest);

        // Assert: Registration should succeed
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Act 2: Attempt to login with the same credentials on Acme tenant
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = JsonContent.Create(new { email = userEmail, password })
        };
        loginRequest.Headers.Add("X-Tenant-ID", "acme");

        var loginResponse = await _client!.SendAsync(loginRequest);

        // Assert: Login should FAIL because user is registered on Default, not Acme
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized)
            ;
    }
}
