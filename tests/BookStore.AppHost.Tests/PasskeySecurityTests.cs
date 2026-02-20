using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Identity;
using Refit;
using TUnit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Integration tests for passkey security features:
/// - Credential counter validation (cloned authenticator detection)
/// - Security stamp validation (token revocation)
/// - Cross-tenant token theft detection
/// - Refresh token cleanup on passkey login
/// - Multi-tenant passkey isolation
/// </summary>
public class PasskeySecurityTests
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
    public async Task PasskeyLogin_WithClonedAuthenticator_LocksAccount()
    {
        // Arrange: Register user and attach a real passkey via the virtual authenticator
        var (email, _, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        var passkey = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        // Read the credential ID from the DB so we can manipulate its sign counter
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        byte[] credentialId;
        await using (var readSession = store.LightweightSession(tenantId))
        {
            var u = await DatabaseHelpers.GetUserByEmailAsync(readSession, email);
            _ = await Assert.That(u).IsNotNull();
            credentialId = u!.Passkeys.First().CredentialId;
        }

        // Inflate the stored counter far above what the virtual authenticator will produce next
        // (virtual authenticator counter ~= 1 on first assertion, 100 >> 1 → mismatch → lockout)
        await PasskeyTestHelpers.UpdatePasskeySignCountAsync(tenantId, email, credentialId, signCount: 100);

        // Act: Attempt passkey login — assertion counter (~1) ≤ stored (100) → should trigger lockout
        _ = await Assert.That(
            async () => await webAuthn.LoginWithPasskeyAsync(passkey))
            .Throws<Exception>();

        // Assert: Account should now be locked in the database
        var isLocked = false;
        await SseEventHelpers.WaitForConditionAsync(
            async () =>
            {
                await using var lockSession = store.LightweightSession(tenantId);
                var lockedUser = await DatabaseHelpers.GetUserByEmailAsync(lockSession, email);
                isLocked = lockedUser?.LockoutEnd != null && lockedUser.LockoutEnd.Value > DateTimeOffset.UtcNow;
                return isLocked;
            },
            TimeSpan.FromSeconds(5),
            "Account was not locked after counter-mismatch passkey assertion");

        _ = await Assert.That(isLocked).IsTrue();
    }

    [Test]
    public async Task Token_AfterSecurityStampChange_BecomesInvalid()
    {
        // Arrange - Register and login to get a valid token
        var (email, password, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(loginResponse.AccessToken, tenantId));

        // Verify token works initially
        var initialStatus = await authClient.GetPasswordStatusAsync();
        _ = await Assert.That(initialStatus).IsNotNull();

        // Act - Change password (this updates security stamp)
        await authClient.ChangePasswordAsync(new ChangePasswordRequest(
            password,
            FakeDataGenerators.GenerateFakePassword()
        ));

        // Assert - Old token should now be rejected due to security stamp mismatch
        var exception = await Assert.That(async () =>
            await authClient.GetPasswordStatusAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_AfterAddingPasskey_BecomesInvalid()
    {
        // Arrange
        var (email, password, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(loginResponse.AccessToken, tenantId));

        // Verify token works initially
        var initialStatus = await authClient.GetPasswordStatusAsync();
        _ = await Assert.That(initialStatus).IsNotNull();

        // Act - Add a passkey via the real WebAuthn endpoint (which calls UpdateSecurityStampAsync internally)
        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        _ = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        // Assert - Old token should now be rejected
        var exception = await Assert.That(async () =>
            await authClient.GetPasswordStatusAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshToken_FromDifferentTenant_LocksAccountAndClearsTokens()
    {
        // Arrange - Create a user in a unique tenant per run
        var tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant1);
        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var client1 = HttpClientHelpers.GetUnauthenticatedClient(tenant1);
        var register1 = await client1.PostAsJsonAsync("/account/register", new { email, password });
        _ = register1.EnsureSuccessStatusCode();

        // Get refresh token for tenant1
        var login1 = await client1.PostAsJsonAsync("/account/login", new { email, password });
        _ = login1.EnsureSuccessStatusCode();
        var loginResponse1 = await login1.Content.ReadFromJsonAsync<AuthenticationHelpers.LoginResponse>();

        // Manually add the same refresh token with a DIFFERENT tenant ID to simulate cross-tenant token theft
        // In a real scenario, this would require a security breach or bug that allows token reuse across tenants
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using (var session = store.LightweightSession(tenant1))
        {
            var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);

            if (user != null)
            {
                // Add a token with wrong tenant ID (simulating the attack scenario the code defends against)
                // Use a different generated tenant ID to simulate the attack — it doesn't need to be a real tenant
                var spoofedTenantId = FakeDataGenerators.GenerateFakeTenantId();
                user.RefreshTokens.Add(new RefreshTokenInfo(
                    Token: "malicious-token-123",
                    Expires: DateTimeOffset.UtcNow.AddDays(7),
                    Created: DateTimeOffset.UtcNow,
                    TenantId: spoofedTenantId, // Different tenant!
                    SecurityStamp: user.SecurityStamp ?? string.Empty
                ));
                session.Update(user);
                await session.SaveChangesAsync();
            }
        }

        // Act - Try to use the malicious token (this should trigger cross-tenant detection)
        var refreshResponse = await client1.PostAsJsonAsync("/account/refresh-token", new
        {
            refreshToken = "malicious-token-123"
        });

        // Assert - Should be rejected with 403 Forbidden (user is authenticated but not permitted across tenants)
        _ = await Assert.That(refreshResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);

        // Verify tenant1 account is now locked and all tokens cleared
        await using (var sessionVerify = store.LightweightSession(tenant1))
        {
            var user = await DatabaseHelpers.GetUserByEmailAsync(sessionVerify, email);

            _ = await Assert.That(user).IsNotNull();
            _ = await Assert.That(user!.LockoutEnd).IsNotNull();
            _ = await Assert.That(user.LockoutEnd!.Value).IsGreaterThan(DateTimeOffset.UtcNow);
            _ = await Assert.That(user.RefreshTokens).IsEmpty();
        }
    }

    [Test]
    public async Task PasskeyLogin_ClearsAllExistingRefreshTokens()
    {
        // Arrange: Create user and establish 3 separate password sessions (3 refresh tokens)
        var (email, password, login1, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        var client = HttpClientHelpers.GetUnauthenticatedClient(tenantId);
        var loginResponse2 = await client.PostAsJsonAsync("/account/login", new { email, password });
        var login2 = await loginResponse2.Content.ReadFromJsonAsync<AuthenticationHelpers.LoginResponse>();
        var loginResponse3 = await client.PostAsJsonAsync("/account/login", new { email, password });
        var login3 = await loginResponse3.Content.ReadFromJsonAsync<AuthenticationHelpers.LoginResponse>();

        // Register a real passkey using the access token from the first session
        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        var passkey = await webAuthn.RegisterPasskeyAsync(email, tenantId, login1.AccessToken);

        // Act: Perform a real passkey login — this MUST clear all existing refresh tokens
        var passkeyLogin = await webAuthn.LoginWithPasskeyAsync(passkey);
        _ = await Assert.That(passkeyLogin).IsNotNull();

        // Assert: All 3 old refresh tokens should now be rejected
        var response1 = await client.PostAsJsonAsync("/account/refresh-token", new { refreshToken = login1.RefreshToken });
        var response2 = await client.PostAsJsonAsync("/account/refresh-token", new { refreshToken = login2!.RefreshToken });
        var response3 = await client.PostAsJsonAsync("/account/refresh-token", new { refreshToken = login3!.RefreshToken });

        _ = await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        _ = await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        _ = await Assert.That(response3.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Verify the DB contains only the new passkey-login refresh token, not the old ones
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var sessionFinal = store.LightweightSession(tenantId);
        var userAfter = await DatabaseHelpers.GetUserByEmailAsync(sessionFinal, email);

        _ = await Assert.That(userAfter).IsNotNull();
        var oldTokens = userAfter!.RefreshTokens
            .Where(t => t.Token == login1.RefreshToken
                     || t.Token == login2.RefreshToken
                     || t.Token == login3.RefreshToken)
            .ToList();
        _ = await Assert.That(oldTokens).IsEmpty();
    }

    [Test]
    [Arguments("tenant-a", "tenant-b")]
    [Arguments("tenant-a", "default")]
    [Arguments("tenant-b", "default")]
    public async Task ConcurrentPasskeyRegistrations_SameEmailDifferentTenants_Succeed(string tenant1, string tenant2)
    {
        // Arrange: Use the same email for both tenants
        var sharedEmail = FakeDataGenerators.GenerateFakeEmail();

        // Create users in both tenants with the same email
        var (_, _, login1, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenant1, sharedEmail);
        var (_, _, login2, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenant2, sharedEmail);

        // Act: Add passkeys to both users (same email, different tenants)
        var credentialId1 = Guid.CreateVersion7().ToByteArray();
        var credentialId2 = Guid.CreateVersion7().ToByteArray();

        await PasskeyTestHelpers.AddPasskeyToUserAsync(tenant1, sharedEmail, "Tenant1 Passkey", credentialId1);
        await PasskeyTestHelpers.AddPasskeyToUserAsync(tenant2, sharedEmail, "Tenant2 Passkey", credentialId2);

        // Assert: Both passkeys should exist in their respective tenants
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();

        await using (var session1 = store.LightweightSession(tenant1))
        {
            var user1 = await DatabaseHelpers.GetUserByEmailAsync(session1, sharedEmail);
            _ = await Assert.That(user1).IsNotNull();
            _ = await Assert.That(user1!.Passkeys.Count).IsEqualTo(1);
            _ = await Assert.That(user1.Passkeys[0].Name).IsEqualTo("Tenant1 Passkey");
        }

        await using var session2 = store.LightweightSession(tenant2);
        var user2 = await DatabaseHelpers.GetUserByEmailAsync(session2, sharedEmail);
        _ = await Assert.That(user2).IsNotNull();
        _ = await Assert.That(user2!.Passkeys.Count).IsEqualTo(1);
        _ = await Assert.That(user2.Passkeys[0].Name).IsEqualTo("Tenant2 Passkey");
    }

    [Test]
    [Arguments("default")]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task UserWithPasskeyOnly_CanAccessProtectedEndpoints(string tenantId)
    {
        // Arrange: Create user with password
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);

        // Register a passkey via the real WebAuthn endpoint (security stamp changes)
        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        var passkey = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        // Get a fresh JWT via password after the security-stamp change
        var unauthClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));
        var freshLogin = await unauthClient.LoginAsync(new LoginRequest(email, password));

        // Remove password — user is now passkey-only (security stamp changes again)
        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(freshLogin.AccessToken, tenantId));
        await authClient.RemovePasswordAsync(new RemovePasswordRequest());

        // Act: Login via passkey as a passkey-only user and access a protected endpoint
        var passkeyLogin = await webAuthn.LoginWithPasskeyAsync(passkey);
        var passkeyAuthClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(passkeyLogin.AccessToken, tenantId));

        var passwordStatus = await passkeyAuthClient.GetPasswordStatusAsync();

        // Assert: Endpoint is accessible and reports no password
        _ = await Assert.That(passwordStatus).IsNotNull();
        _ = await Assert.That(passwordStatus.HasPassword).IsFalse();
    }

    [Test]
    public async Task UserWithPasswordOnly_CanAccessBasicEndpoints()
    {
        // Arrange: Create user with password only (no passkey)
        var (email, password, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        // Verify user has no passkeys
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using (var session = store.LightweightSession(tenantId))
        {
            var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
            _ = await Assert.That(user).IsNotNull();
            _ = await Assert.That(user!.Passkeys).IsEmpty();
            _ = await Assert.That(user.PasswordHash).IsNotNull();
        }

        // Act: Access passkey endpoints - should work (list will be empty)
        var passkeyClient = RestService.For<IPasskeyClient>(
            HttpClientHelpers.GetAuthenticatedClient(loginResponse.AccessToken, tenantId));

        var passkeys = await passkeyClient.ListPasskeysAsync();

        // Assert: User can list passkeys (empty list) even without having any
        _ = await Assert.That(passkeys).IsEmpty();

        // User can access password management endpoints
        var identityClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(loginResponse.AccessToken, tenantId));

        var passwordStatus = await identityClient.GetPasswordStatusAsync();
        _ = await Assert.That(passwordStatus).IsNotNull();
        _ = await Assert.That(passwordStatus.HasPassword).IsTrue();
    }

    [Test]
    public async Task CannotDeleteLastPasskey_WithoutPassword()
    {
        // Arrange: Create user with password
        var (email, password, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        // Register a passkey via the real WebAuthn endpoint (security stamp changes)
        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        var passkey = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        // Get a fresh JWT via password after the security-stamp change
        var unauthClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));
        var freshLogin = await unauthClient.LoginAsync(new LoginRequest(email, password));

        // Remove password — user is now passkey-only (security stamp changes again)
        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(freshLogin.AccessToken, tenantId));
        await authClient.RemovePasswordAsync(new RemovePasswordRequest());

        // Act: Login via passkey and try to delete the last (and only) passkey
        var passkeyLogin = await webAuthn.LoginWithPasskeyAsync(passkey);
        var passkeyClient = RestService.For<IPasskeyClient>(
            HttpClientHelpers.GetAuthenticatedClient(passkeyLogin.AccessToken, tenantId));

        var passkeys = await passkeyClient.ListPasskeysAsync();
        _ = await Assert.That(passkeys.Count).IsEqualTo(1);

        var lastPasskeyId = passkeys[0].Id;

        // Assert: Deleting the only passkey when the user has no password is rejected
        var exception = await Assert.That(async () =>
            await passkeyClient.DeletePasskeyAsync(lastPasskeyId))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        // The passkey must still be present
        var passkeysAfter = await passkeyClient.ListPasskeysAsync();
        _ = await Assert.That(passkeysAfter.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SecurityStamp_InToken_MustMatchUserSecurityStamp()
    {
        // Arrange - Register user and get token
        var (email, _, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        // Verify token contains security_stamp claim
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(loginResponse.AccessToken);
        var securityStampClaim = token.Claims.FirstOrDefault(c => c.Type == "security_stamp");
        _ = await Assert.That(securityStampClaim).IsNotNull();

        // Verify it matches the user's actual security stamp
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);
        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);

        _ = await Assert.That(user).IsNotNull();
        _ = await Assert.That(securityStampClaim!.Value).IsEqualTo(user!.SecurityStamp);
    }

    [Test]
    public async Task PasskeySignCount_MustBeStoredAndIncrement()
    {
        // Arrange: Register user and attach a real passkey via the virtual authenticator
        var (email, _, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        var passkey = await webAuthn.RegisterPasskeyAsync(email, tenantId, loginResponse.AccessToken);

        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();

        // Helper: read the stored sign count for this passkey from the DB
        async Task<uint> GetStoredSignCountAsync()
        {
            await using var session = store.LightweightSession(tenantId);
            var u = await DatabaseHelpers.GetUserByEmailAsync(session, email);
            return u!.Passkeys.First().SignCount;
        }

        var countAfterRegistration = await GetStoredSignCountAsync();

        // Act: First passkey login — virtual authenticator produces counter 1
        _ = await webAuthn.LoginWithPasskeyAsync(passkey);
        var countAfterLogin1 = await GetStoredSignCountAsync();

        // Second passkey login — virtual authenticator produces counter 2
        _ = await webAuthn.LoginWithPasskeyAsync(passkey);
        var countAfterLogin2 = await GetStoredSignCountAsync();

        // Assert: counter must strictly increase with each successful assertion
        _ = await Assert.That(countAfterLogin1).IsGreaterThan(countAfterRegistration);
        _ = await Assert.That(countAfterLogin2).IsGreaterThan(countAfterLogin1);
    }
}
