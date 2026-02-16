using System.Net;
using Bogus;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Refit;
using TUnit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class EmailVerificationTests
{
    readonly IIdentityClient _client;
    readonly Faker _faker;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Ensure tenants exist for data-driven tests
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

        await DatabaseHelpers.SeedTenantAsync(store, "tenant-a");
        await DatabaseHelpers.SeedTenantAsync(store, "tenant-b");
    }

    public EmailVerificationTests()
    {
        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        _client = RestService.For<IIdentityClient>(httpClient);
        _faker = new Faker();
    }

    [Test]
    public async Task EmailVerification_FullFlow_ShouldSucceed()
    {
        // 1. Register a new user
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");
        var registerRequest = new RegisterRequest(email, password);

        _ = await _client.RegisterAsync(registerRequest);

        // Manually unconfirm email to simulate verification requirement (since tests use DeliveryMethod=None)
        await ManuallySetEmailConfirmedAsync(email, false);

        // 2. Attempt login - should fail with Requires verification
        try
        {
            _ = await _client.LoginAsync(new LoginRequest(email, password));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.Unauthorized);
            var problem = await ex.GetContentAsAsync<AuthenticationHelpers.ValidationProblemDetails>();
            _ = await Assert.That(problem?.Error).IsEqualTo(ErrorCodes.Auth.EmailUnconfirmed);
        }

        // 3. Resend verification
        await _client.ResendVerificationAsync(new ResendVerificationRequest(email));

        // 4. Manually confirm email via DB (simulating clicking the link)
        await ManuallySetEmailConfirmedAsync(email, true);

        // 5. Attempt login again - should succeed
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));
        _ = await Assert.That(loginResult.AccessToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task ResendVerification_ForNonExistentUser_ShouldReturnGenericSuccess()
        // Act - Identity API returns 200 OK even for non-existent users to prevent enumeration
        => await _client.ResendVerificationAsync(new ResendVerificationRequest("nonexistent@example.com"));

    [Test]
    public async Task ResendVerification_ForAlreadyConfirmedUser_ShouldReturnGenericSuccess()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        // Confirm (it's already true by default in tests, but let's be explicit)
        await ManuallySetEmailConfirmedAsync(email, true);

        // Act
        await _client.ResendVerificationAsync(new ResendVerificationRequest(email));
    }

    [Test]
    public async Task ResendVerification_ShouldEnforceCooldown()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        await ManuallySetEmailConfirmedAsync(email, false);

        // Act 1: First resend
        await _client.ResendVerificationAsync(new ResendVerificationRequest(email));

        // Check timestamp was set
        var userAfterFirst = await GetUserAsync(email);
        _ = await Assert.That(userAfterFirst?.LastVerificationEmailSent).IsNotNull();
        var timestamp1 = userAfterFirst!.LastVerificationEmailSent;

        // Act 2: Immediate second resend
        await _client.ResendVerificationAsync(new ResendVerificationRequest(email));

        // Check timestamp was NOT updated (cooldown strictly enforced)
        var userAfterSecond = await GetUserAsync(email);
        _ = await Assert.That(userAfterSecond?.LastVerificationEmailSent).IsEqualTo(timestamp1);
    }

    [Test]
    [Arguments("default")]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task LoginAttempt_WithUnconfirmedEmail_ReturnsEmailUnconfirmedError(string tenantId)
    {
        // Arrange: Register user in specific tenant
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        var tenantClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));
        _ = await tenantClient.RegisterAsync(new RegisterRequest(email, password));

        // Manually set email to unconfirmed
        await ManuallySetEmailConfirmedAsync(email, false, tenantId);

        // Act: Attempt login with unconfirmed email
        var exception = await Assert.That(async () =>
            await tenantClient.LoginAsync(new LoginRequest(email, password)))
            .Throws<ApiException>();

        // Assert: Should return ERR_AUTH_EMAIL_UNCONFIRMED
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        var problem = await exception.GetContentAsAsync<AuthenticationHelpers.ValidationProblemDetails>();
        _ = await Assert.That(problem?.Error).IsEqualTo(ErrorCodes.Auth.EmailUnconfirmed);
    }

    [Test]
    public async Task EmailConfirmation_WithInvalidToken_ReturnsError()
    {
        // Arrange: Register a user
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));
        await ManuallySetEmailConfirmedAsync(email, false);

        // Act: Try to confirm with completely invalid token
        var invalidToken = "invalid-token-12345";

        // Note: ConfirmEmailAsync takes userId and code as query parameters, but we don't have userId
        // So this test verifies the endpoint returns error for invalid input
        var exception = await Assert.That(async () =>
            await _client.ConfirmEmailAsync(Guid.Empty.ToString(), invalidToken))
            .Throws<ApiException>();

        // Assert: Should return bad request or unauthorized
        var isExpectedError = exception!.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isExpectedError).IsTrue();
    }

    [Test]
    [Arguments("default")]
    [Arguments("tenant-a")]
    public async Task ResendVerification_RespectsCooldownBoundary(string tenantId)
    {
        // Arrange: Register and set up for verification in specific tenant
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        var tenantClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));
        _ = await tenantClient.RegisterAsync(new RegisterRequest(email, password));
        await ManuallySetEmailConfirmedAsync(email, false, tenantId);

        // Act 1: First resend
        await tenantClient.ResendVerificationAsync(new ResendVerificationRequest(email));
        var userAfterFirst = await GetUserAsync(email, tenantId);
        var timestamp1 = userAfterFirst!.LastVerificationEmailSent;
        _ = await Assert.That(timestamp1).IsNotNull();

        // Act 2: Immediate second resend (within cooldown)
        await tenantClient.ResendVerificationAsync(new ResendVerificationRequest(email));
        var userAfterSecond = await GetUserAsync(email, tenantId);
        _ = await Assert.That(userAfterSecond!.LastVerificationEmailSent).IsEqualTo(timestamp1);

        // Act 3: Wait past cooldown (60 seconds + buffer) and resend again
        await ManuallySetVerificationTimestampAsync(email, tenantId, DateTimeOffset.UtcNow.AddSeconds(-61));
        await tenantClient.ResendVerificationAsync(new ResendVerificationRequest(email));
        var userAfterCooldown = await GetUserAsync(email, tenantId);

        // Assert: Timestamp should be updated after cooldown period  
        _ = await Assert.That(userAfterCooldown!.LastVerificationEmailSent!.Value).IsGreaterThan(timestamp1!.Value);
    }

    async Task<IDocumentStore> GetStoreAsync()
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

    async Task<ApplicationUser?> GetUserAsync(string email, string? tenantId = null)
    {
        using var store = await GetStoreAsync();
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        await using var session = store.LightweightSession(actualTenantId);
        var normalizedEmail = email.ToUpperInvariant();
        return await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();
    }

    async Task ManuallySetEmailConfirmedAsync(string email, bool confirmed, string? tenantId = null)
    {
        using var store = await GetStoreAsync();
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        await using var session = store.LightweightSession(actualTenantId);

        var normalizedEmail = email.ToUpperInvariant();
        var user = await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();

        _ = await Assert.That(user).IsNotNull(); // Ensure user exists

        if (user != null)
        {
            user.EmailConfirmed = confirmed;
            session.Store(user);
            await session.SaveChangesAsync();
        }
    }

    async Task ManuallySetVerificationTimestampAsync(string email, string tenantId, DateTimeOffset timestamp)
    {
        using var store = await GetStoreAsync();
        await using var session = store.LightweightSession(tenantId);

        var normalizedEmail = email.ToUpperInvariant();
        var user = await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();

        _ = await Assert.That(user).IsNotNull();

        if (user != null)
        {
            user.LastVerificationEmailSent = timestamp;
            session.Store(user);
            await session.SaveChangesAsync();
        }
    }
}
