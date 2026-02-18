using System.IdentityModel.Tokens.Jwt;
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
/// Tests to verify security stamp validation invalidates JWTs after credential changes.
/// CRITICAL: These tests validate that tokens are properly invalidated when security-sensitive operations occur.
/// </summary>
public class SecurityStampValidationTests
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
    public async Task JWT_WithMismatchedSecurityStamp_IsRejected(string tenantId)
    {
        // Arrange: Create user and get JWT
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);
        var oldAccessToken = loginResponse.AccessToken;

        // Manually update security stamp in database (simulating credential change)
        await ManuallyUpdateSecurityStampAsync(email, tenantId);

        // Small delay to ensure database transaction is committed
        await Task.Delay(100);

        //Act: Try to access protected endpoint with old JWT
        var booksClient = RestService.For<IBooksClient>(
            HttpClientHelpers.GetAuthenticatedClient(oldAccessToken, tenantId));

        var exception = await Assert.That(async () =>
            await booksClient.GetFavoriteBooksAsync(new OrderedPagedRequest()))
            .Throws<ApiException>();

        // Assert: Should return unauthorized due to security stamp mismatch
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AccessProtectedEndpoint_AfterPasswordChange_OldJWTRejected()
    {
        // Arrange: Create user and get JWT
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();
        var oldAccessToken = loginResponse.AccessToken;

        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(oldAccessToken));

        // Act 1: Change password (updates security stamp)
        var newPassword = FakeDataGenerators.GenerateFakePassword();
        await authClient.ChangePasswordAsync(new ChangePasswordRequest(password, newPassword));

        // Act 2: Try to access protected endpoint with old JWT
        var booksClient = RestService.For<IBooksClient>(
            HttpClientHelpers.GetAuthenticatedClient(oldAccessToken));

        var exception = await Assert.That(async () =>
            await booksClient.GetFavoriteBooksAsync(new OrderedPagedRequest()))
            .Throws<ApiException>();

        // Assert: Old JWT should be rejected
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Assert: New login with new password should work
        var unauthClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
        var newLoginResponse = await unauthClient.LoginAsync(new LoginRequest(email, newPassword));
        _ = await Assert.That(newLoginResponse).IsNotNull();
        _ = await Assert.That(newLoginResponse.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task SecurityStampUpdate_InvalidatesAllExistingJWTs()
    {
        // Arrange: Create user and get multiple JWTs
        var (email, password, loginResponse1, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
        var loginResponse2 = await client.LoginAsync(new LoginRequest(email, password));
        var loginResponse3 = await client.LoginAsync(new LoginRequest(email, password));

        var jwt1 = loginResponse1.AccessToken;
        var jwt2 = loginResponse2.AccessToken;
        var jwt3 = loginResponse3.AccessToken;

        // Act: Update security stamp
        await ManuallyUpdateSecurityStampAsync(email);

        // Small delay to ensure database transaction is committed
        await Task.Delay(100);

        // Assert: All three JWTs should be rejected
        var booksClient1 = RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(jwt1));
        var exception1 = await Assert.That(async () => await booksClient1.GetFavoriteBooksAsync(new OrderedPagedRequest())).Throws<ApiException>();
        _ = await Assert.That(exception1!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var booksClient2 = RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(jwt2));
        var exception2 = await Assert.That(async () => await booksClient2.GetFavoriteBooksAsync(new OrderedPagedRequest())).Throws<ApiException>();
        _ = await Assert.That(exception2!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var booksClient3 = RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(jwt3));
        var exception3 = await Assert.That(async () => await booksClient3.GetFavoriteBooksAsync(new OrderedPagedRequest())).Throws<ApiException>();
        _ = await Assert.That(exception3!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    public async Task AddPasskey_UpdatesSecurityStamp_InvalidatesOldJWT(string tenantId)
    {
        // Arrange: Create user and get JWT
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenantId);
        var oldAccessToken = loginResponse.AccessToken;

        // Act: Add a passkey (this updates security stamp via PasskeyTestHelpers)
        var credentialId = Guid.CreateVersion7().ToByteArray();
        await PasskeyTestHelpers.AddPasskeyToUserAsync(tenantId, email, "New Passkey", credentialId);

        // Small delay to ensure database transaction is committed
        await Task.Delay(100);

        // Assert: Old JWT should be rejected
        var booksClient = RestService.For<IBooksClient>(
            HttpClientHelpers.GetAuthenticatedClient(oldAccessToken, tenantId));

        var exception = await Assert.That(async () =>
            await booksClient.GetFavoriteBooksAsync(new OrderedPagedRequest()))
            .Throws<ApiException>();

        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RemovePassword_UpdatesSecurityStamp_InvalidatesOldJWT()
    {
        // Arrange: Create user with both password and passkey
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();
        var oldAccessToken = loginResponse.AccessToken;

        // Add passkey first (required before removing password)
        var credentialId = Guid.CreateVersion7().ToByteArray();
        await PasskeyTestHelpers.AddPasskeyToUserAsync(StorageConstants.DefaultTenantId, email, "Passkey", credentialId);

        // Get new JWT after adding passkey (old one is now invalid)
        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
        var newLoginResponse = await client.LoginAsync(new LoginRequest(email, password));
        var newAccessToken = newLoginResponse.AccessToken;

        var authClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(newAccessToken));

        // Act: Remove password (updates security stamp)
        await authClient.RemovePasswordAsync(new RemovePasswordRequest());

        // Small delay to ensure database transaction is committed
        await Task.Delay(100);

        // Assert: JWT before password removal should be rejected
        var booksClient = RestService.For<IBooksClient>(
            HttpClientHelpers.GetAuthenticatedClient(newAccessToken));

        var exception = await Assert.That(async () =>
            await booksClient.GetFavoriteBooksAsync(new OrderedPagedRequest()))
            .Throws<ApiException>();

        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SecurityStampClaim_IsPresentInJWT()
    {
        // Arrange & Act: Create user and get JWT
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync();
        var accessToken = loginResponse.AccessToken;

        // Parse JWT
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);

        // Assert: security_stamp claim should be present
        var securityStampClaim = jwt.Claims.FirstOrDefault(c => c.Type == "security_stamp");
        _ = await Assert.That(securityStampClaim).IsNotNull();
        _ = await Assert.That(securityStampClaim!.Value).IsNotEmpty();
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

    async Task ManuallyUpdateSecurityStampAsync(string email, string? tenantId = null)
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
            user.SecurityStamp = Guid.CreateVersion7().ToString();
            session.Store(user);
            await session.SaveChangesAsync();
        }
    }
}
