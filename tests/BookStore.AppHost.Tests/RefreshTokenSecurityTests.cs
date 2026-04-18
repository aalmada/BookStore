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
/// Tests to verify refresh token security and invalidation scenarios.
/// CRITICAL: These tests validate token rotation, security stamp validation, and expiration.
/// </summary>
public class RefreshTokenSecurityTests
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
    public async Task ExpiredRefreshToken_ReturnsTokenInvalidError()
    {
        // Arrange: Create user and get tokens
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();
        var refreshToken = loginResponse.RefreshToken;
        var accessToken = loginResponse.AccessToken;

        // Manually expire the refresh token in database
        await ManuallyExpireRefreshTokenAsync(email, refreshToken);

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetAuthenticatedClient(accessToken));

        // Act: Try to use expired refresh token
        var exception = await Assert.That(async () =>
            await client.RefreshTokenAsync(new RefreshRequest(refreshToken)))
            .Throws<ApiException>();

        // Assert: Should return unauthorized or bad request
        var isExpectedError = exception!.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest;
        _ = await Assert.That(isExpectedError).IsTrue();
    }

    [Test]
    [Arguments("default")]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task RefreshToken_AfterPasswordChange_BecomesInvalid(string tenantId)
    {
        // Arrange: Create user and get tokens
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);
        var refreshToken = loginResponse.RefreshToken;
        var accessToken = loginResponse.AccessToken;

        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(accessToken, tenantId));

        // Act 1: Change password (this updates security stamp)
        var newPassword = FakeDataGenerators.GenerateFakePassword();
        await authClient.ChangePasswordAsync(new ChangePasswordRequest(password, newPassword));

        // Act 2: Try to use old refresh token (should fail due to security stamp change)
        var exception = await Assert.That(async () =>
            await authClient.RefreshTokenAsync(new RefreshRequest(refreshToken)))
            .Throws<ApiException>();

        // Assert: Should be rejected
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshToken_AfterPasskeyAddition_BecomesInvalid()
    {
        // Arrange: Create user and get tokens
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();
        var refreshToken = loginResponse.RefreshToken;
        var accessToken = loginResponse.AccessToken;
        var userId = loginResponse.UserId;

        // Add a passkey (this updates security stamp per PasskeyEndpoints.cs#L263-L264)
        var credentialId = Guid.CreateVersion7().ToByteArray();
        await PasskeyTestHelpers.AddPasskeyToUserAsync(StorageConstants.DefaultTenantId, email, "New Passkey", credentialId);

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetAuthenticatedClient(accessToken));

        // Act: Try to use old refresh token (should fail due to security stamp change)
        var exception = await Assert.That(async () =>
            await client.RefreshTokenAsync(new RefreshRequest(refreshToken)))
            .Throws<ApiException>();

        // Assert: Should be rejected
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task TokenRotation_ReusingOldRefreshToken_Fails()
    {
        // Arrange: Create user and get initial tokens
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();
        var oldRefreshToken = loginResponse.RefreshToken;
        var oldAccessToken = loginResponse.AccessToken;

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetAuthenticatedClient(oldAccessToken));

        // Act 1: Use refresh token to get new tokens (rotation)
        var refreshResult = await client.RefreshTokenAsync(new RefreshRequest(oldRefreshToken));
        _ = await Assert.That(refreshResult).IsNotNull();
        _ = await Assert.That(refreshResult.RefreshToken).IsNotEqualTo(oldRefreshToken);

        // Act 2: Try to reuse the old refresh token (should fail - token already rotated)
        var exception = await Assert.That(async () =>
            await client.RefreshTokenAsync(new RefreshRequest(oldRefreshToken)))
            .Throws<ApiException>();

        // Assert: Old token should be invalid after rotation
        var isExpectedError = exception!.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest;
        _ = await Assert.That(isExpectedError).IsTrue();
    }

    [Test]
    public async Task PasswordLogin_ClearsExistingRefreshTokens()
    {
        // Arrange: Use a unique tenant per run to avoid parallel contamination
        var tenantId = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenantId);

        var (email, password, firstLogin, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);

        var unauthClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));

        // Act: Perform additional password logins. Each login should invalidate previous refresh tokens.
        var secondLogin = await unauthClient.LoginAsync(new LoginRequest(email, password));
        var thirdLogin = await unauthClient.LoginAsync(new LoginRequest(email, password));

        // Assert: Prior refresh tokens are rejected
        var firstException = await Assert.That(async () =>
            await unauthClient.RefreshTokenAsync(new RefreshRequest(firstLogin.RefreshToken)))
            .Throws<ApiException>();

        _ = await Assert.That(firstException!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var secondException = await Assert.That(async () =>
            await unauthClient.RefreshTokenAsync(new RefreshRequest(secondLogin.RefreshToken)))
            .Throws<ApiException>();

        _ = await Assert.That(secondException!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Assert: The latest refresh token remains valid
        var latestRefresh = await unauthClient.RefreshTokenAsync(new RefreshRequest(thirdLogin.RefreshToken));
        _ = await Assert.That(latestRefresh).IsNotNull();
    }

    async Task ManuallyExpireRefreshTokenAsync(string email, string refreshToken, string? tenantId = null)
    {
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        await using var session = store.LightweightSession(actualTenantId);

        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
        _ = await Assert.That(user).IsNotNull();

        if (user != null)
        {
            var token = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken);
            if (token != null)
            {
                // RefreshTokenInfo is immutable, so we need to replace it
                _ = user.RefreshTokens.Remove(token);
                user.RefreshTokens.Add(token with { Expires = DateTimeOffset.UtcNow.AddDays(-1) });
                session.Store(user);
                await session.SaveChangesAsync();
            }
        }
    }
}
