using System.Net;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Refit;
using TUnit;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Tests to verify account lockout behavior for both password and passkey authentication.
/// CRITICAL: These tests validate that accounts are locked after repeated failures and lockout persists.
/// </summary>
public class AccountLockoutTests
{
    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        await DatabaseHelpers.CreateTenantViaApiAsync("tenant-a");
        await DatabaseHelpers.CreateTenantViaApiAsync("tenant-b");
    }

    [Test]
    [Arguments("default")]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task FailedPasswordAttempts_TriggerAccountLockout(string tenantId)
    {
        // Arrange: Create user
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));
        _ = await client.RegisterAsync(new RegisterRequest(email, password));

        // Act: Attempt login with wrong password 5 times (configured max attempts)
        var wrongPassword = "WrongPassword123!";
        for (var i = 0; i < 5; i++)
        {
            try
            {
                _ = await client.LoginAsync(new LoginRequest(email, wrongPassword));
            }
            catch (ApiException)
            {
                // Expected to fail
            }
        }

        // Wait for lockout to be reflected
        _ = await WaitForAccountLockoutAsync(email, tenantId);

        // Act: Try to login with CORRECT password after lockout
        var exception = await Assert.That(async () =>
            await client.LoginAsync(new LoginRequest(email, password)))
            .Throws<ApiException>();

        // Assert: Should return locked out error
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        var problem = await exception.GetContentAsAsync<AuthenticationHelpers.ValidationProblemDetails>();
        _ = await Assert.That(problem?.Error).IsEqualTo(ErrorCodes.Auth.LockedOut);
    }

    [Test]
    public async Task LockedAccount_PreventsPasswordLogin()
    {
        // Arrange: Create user and manually lock account
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
        _ = await client.RegisterAsync(new RegisterRequest(email, password));

        // Manually lock the account
        await ManuallyLockAccountAsync(email, DateTimeOffset.UtcNow.AddMinutes(5));

        // Act: Try to login with correct password
        var exception = await Assert.That(async () =>
            await client.LoginAsync(new LoginRequest(email, password)))
            .Throws<ApiException>();

        // Assert: Should return locked out error
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        var problem = await exception.GetContentAsAsync<AuthenticationHelpers.ValidationProblemDetails>();
        _ = await Assert.That(problem?.Error).IsEqualTo(ErrorCodes.Auth.LockedOut);
    }

    [Test]
    public async Task LockedAccount_PreventsPasskeyAuthentication()
    {
        // Arrange: Create user and register a real passkey via the WebAuthn virtual authenticator
        var tenantId = StorageConstants.DefaultTenantId;
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);

        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        var registeredPasskey = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        // Manually lock the account directly in the database
        await ManuallyLockAccountAsync(email, DateTimeOffset.UtcNow.AddMinutes(5), tenantId);

        // Act: Try to login using the real passkey assertion flow
        var loginException = await Assert.That(
            async () => await webAuthn.LoginWithPasskeyAsync(registeredPasskey))
            .Throws<Exception>();

        // Assert: Passkey login should be rejected because the account is locked
        _ = await Assert.That(loginException).IsNotNull();
    }

    [Test]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task PasskeyCounterDecrement_TriggersLockout(string tenantId)
    {
        // Arrange: Register a real passkey (virtual authenticator counter starts at 0)
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);

        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        _ = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        // Perform one real passkey login so the DB sign count advances
        _ = await webAuthn.LoginWithPasskeyAsync(new RegisteredPasskey("", email, "", tenantId));

        // Now read the credential ID from the database
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);
        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
        var credentialId = user!.Passkeys.First().CredentialId;
        var currentCount = user.Passkeys.First().SignCount;

        // Artificially inflate the stored counter well above what the authenticator will produce next
        // This simulates detecting a cloned authenticator: stored counter > assertion counter
        await ManuallySetPasskeySignCountAsync(email, credentialId, currentCount + 100, tenantId);

        // Act: Attempt another passkey login — the virtual authenticator will produce a counter <= stored
        _ = await Assert.That(
            async () => await webAuthn.LoginWithPasskeyAsync(new RegisteredPasskey("", email, "", tenantId)))
            .Throws<Exception>();

        // Assert: Account should now be locked
        var isLocked = await WaitForAccountLockoutAsync(email, tenantId);
        _ = await Assert.That(isLocked).IsTrue();
    }

    [Test]
    public async Task LockoutDuration_EnforcesConfiguredTimeout()
    {
        // Arrange: Create user
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
        _ = await client.RegisterAsync(new RegisterRequest(email, password));

        // Lock account with short duration (3 seconds) — generous enough for parallel test load
        await ManuallyLockAccountAsync(email, DateTimeOffset.UtcNow.AddSeconds(3));

        // Act 1: Verify account is locked
        var exception = await Assert.That(async () =>
            await client.LoginAsync(new LoginRequest(email, password)))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Wait for lockout to expire by polling until login succeeds
        LoginResponse? loginResult = null;
        await SseEventHelpers.WaitForConditionAsync(
            async () =>
            {
                try
                {
                    loginResult = await client.LoginAsync(new LoginRequest(email, password));
                    return true;
                }
                catch (ApiException)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10),
            "Account did not unlock within expected duration");

        // Assert: Should succeed after lockout expires
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult.AccessToken).IsNotEmpty();
    }

    async Task ManuallyLockAccountAsync(string email, DateTimeOffset lockoutEnd, string? tenantId = null)
    {
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        await using var session = store.LightweightSession(actualTenantId);

        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
        _ = await Assert.That(user).IsNotNull();

        if (user != null)
        {
            user.LockoutEnd = lockoutEnd;
            session.Store(user);
            await session.SaveChangesAsync();
        }
    }

    async Task<bool> WaitForAccountLockoutAsync(string email, string? tenantId = null)
    {
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        var isLocked = false;

        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await SseEventHelpers.WaitForConditionAsync(
            async () =>
            {
                await using var session = store.LightweightSession(actualTenantId);
                var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
                isLocked = user?.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
                return isLocked;
            },
            TimeSpan.FromSeconds(5),
            $"Account was not locked for {email}");

        return isLocked;
    }

    async Task ManuallySetPasskeySignCountAsync(string email, byte[] credentialId, uint signCount, string? tenantId = null)
    {
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        await using var session = store.LightweightSession(actualTenantId);

        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
        _ = await Assert.That(user).IsNotNull();

        if (user != null)
        {
            var passkey = user.Passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId));
            if (passkey != null)
            {
                passkey.SignCount = signCount;
                session.Store(user);
                await session.SaveChangesAsync();
            }
        }
    }
}
