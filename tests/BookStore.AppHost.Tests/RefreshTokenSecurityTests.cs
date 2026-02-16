using System.Net;
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
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task RefreshToken_KeepsLatestFiveTokens(string tenantId)
    {
        // Arrange: Create user
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);

        var tokens = new List<(string RefreshToken, string AccessToken)>
        {
            (loginResponse.RefreshToken, loginResponse.AccessToken)
        };

        var client = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(loginResponse.AccessToken, tenantId));

        // Act: Rotate tokens 6 times to exceed the limit of 5
        for (var i = 0; i < 6; i++)
        {
            var currentClient = RestService.For<IIdentityClient>(
                HttpClientHelpers.GetAuthenticatedClient(tokens[^1].AccessToken, tenantId));

            var refreshResult = await currentClient.RefreshTokenAsync(
                new RefreshRequest(tokens[^1].RefreshToken));

            tokens.Add((refreshResult.RefreshToken, refreshResult.AccessToken));
        }

        // Assert: First token should be invalid (beyond the 5-token limit)
        var firstTokenClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(tokens[0].AccessToken, tenantId));

        var exception = await Assert.That(async () =>
            await firstTokenClient.RefreshTokenAsync(new RefreshRequest(tokens[0].RefreshToken)))
            .Throws<ApiException>();

        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Assert: One of the recent tokens should still be valid
        var recentTokenClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(tokens[^2].AccessToken, tenantId));

        var validRefresh = await recentTokenClient.RefreshTokenAsync(
            new RefreshRequest(tokens[^2].RefreshToken));

        _ = await Assert.That(validRefresh).IsNotNull();
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

    async Task ManuallyExpireRefreshTokenAsync(string email, string refreshToken, string? tenantId = null)
    {
        using var store = await GetStoreAsync();
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        await using var session = store.LightweightSession(actualTenantId);

        var normalizedEmail = email.ToUpperInvariant();
        var user = await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .FirstOrDefaultAsync();

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
