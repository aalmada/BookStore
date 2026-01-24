using System.Net.Http.Json;
using Bogus;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class EmailVerificationTests
{
    readonly HttpClient _client;
    readonly Faker _faker;

    public EmailVerificationTests()
    {
        _client = TestHelpers.GetUnauthenticatedClient();
        _faker = new Faker();
    }

    [Test]
    public async Task EmailVerification_FullFlow_ShouldSucceed()
    {
        // 1. Register a new user
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        var registerResponse =
            await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Manually unconfirm email to simulate verification requirement (since tests use DeliveryMethod=None)
        await ManuallySetEmailConfirmedAsync(email, false);

        // 2. Attempt login - should fail with Requires verification
        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        _ = await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var loginError = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(loginError?.Error).IsEqualTo(ErrorCodes.Auth.EmailUnconfirmed);

        // 3. Resend verification
        var resendResponse = await _client.PostAsJsonAsync("/account/resend-verification", new { Email = email });
        _ = await Assert.That(resendResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var resendResult = await resendResponse.Content.ReadFromJsonAsync<TestHelpers.MessageResponse>();
        _ = await Assert.That(resendResult?.Message).Contains("a new verification link has been sent");

        // 4. Manually confirm email via DB (simulating clicking the link)
        await ManuallySetEmailConfirmedAsync(email, true);

        // 5. Attempt login again - should succeed
        var loginResponse2 =
            await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        _ = await Assert.That(loginResponse2.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginResult = await loginResponse2.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();
        _ = await Assert.That(loginResult?.AccessToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task ResendVerification_ForNonExistentUser_ShouldReturnGenericSuccess()
    {
        // Act
        var response =
            await _client.PostAsJsonAsync("/account/resend-verification", new { Email = "nonexistent@example.com" });

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestHelpers.MessageResponse>();
        _ = await Assert.That(result?.Message).Contains("If an account exists");
    }

    [Test]
    public async Task ResendVerification_ForAlreadyConfirmedUser_ShouldReturnGenericSuccess()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        // Confirm (it's already true by default in tests, but let's be explicit)
        await ManuallySetEmailConfirmedAsync(email, true);

        // Act
        var response = await _client.PostAsJsonAsync("/account/resend-verification", new { Email = email });

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestHelpers.MessageResponse>();
        _ = await Assert.That(result?.Message).Contains("If an account exists");
    }

    [Test]
    public async Task ResendVerification_ShouldEnforceCooldown()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        await ManuallySetEmailConfirmedAsync(email, false);

        // Act 1: First resend
        var response1 = await _client.PostAsJsonAsync("/account/resend-verification", new { Email = email });
        _ = await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Check timestamp was set
        var userAfterFirst = await GetUserAsync(email);
        _ = await Assert.That(userAfterFirst?.LastVerificationEmailSent).IsNotNull();
        var timestamp1 = userAfterFirst!.LastVerificationEmailSent;

        // Act 2: Immediate second resend
        var response2 = await _client.PostAsJsonAsync("/account/resend-verification", new { Email = email });
        _ = await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.OK); // Should still be OK

        // Check timestamp was NOT updated (cooldown strictly enforced)
        var userAfterSecond = await GetUserAsync(email);
        _ = await Assert.That(userAfterSecond?.LastVerificationEmailSent).IsEqualTo(timestamp1);
    }

    async Task<ApplicationUser?> GetUserAsync(string email)
    {
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await using var session = store.LightweightSession(StorageConstants.DefaultTenantId);
        var normalizedEmail = email.ToUpperInvariant();
        return await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();
    }

    async Task ManuallySetEmailConfirmedAsync(string email, bool confirmed)
    {
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

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
