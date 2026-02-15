using System.Net;
using Bogus;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class EmailVerificationTests
{
    readonly IIdentityClient _client;
    readonly Faker _faker;

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

    async Task<ApplicationUser?> GetUserAsync(string email)
    {
        using var store = await GetStoreAsync();
        await using var session = store.LightweightSession(StorageConstants.DefaultTenantId);
        var normalizedEmail = email.ToUpperInvariant();
        return await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();
    }

    async Task ManuallySetEmailConfirmedAsync(string email, bool confirmed)
    {
        using var store = await GetStoreAsync();
        await using var session = store.LightweightSession(StorageConstants.DefaultTenantId);

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
}
