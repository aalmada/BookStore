using System.Net;
using Bogus;
using BookStore.ApiService.Models;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Identity;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class PasswordManagementTests
{
    readonly IIdentityClient _client;
    readonly Faker _faker;

    public PasswordManagementTests()
    {
        var httpClient = TestHelpers.GetUnauthenticatedClient();
        _client = RestService.For<IIdentityClient>(httpClient);
        _faker = new Faker();
    }

    [Test]
    public async Task GetPasswordStatus_WhenUserHasPassword_ShouldReturnTrue()
    {
        // Arrange
        var identityClient = await TestHelpers.CreateUserAndGetClientAsync<IIdentityClient>();

        // Act
        var status = await identityClient.GetPasswordStatusAsync();

        // Assert
        _ = await Assert.That(status).IsNotNull();
        _ = await Assert.That(status.HasPassword).IsTrue();
    }

    [Test]
    public async Task ChangePassword_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        var email = TestHelpers.GenerateFakeEmail();
        var oldPassword = TestHelpers.GenerateFakePassword();
        var newPassword = TestHelpers.GenerateFakePassword();

        // Register
        _ = await _client.RegisterAsync(new RegisterRequest(email, oldPassword));

        // Login to get token
        var loginResult = await _client.LoginAsync(new LoginRequest(email, oldPassword));

        var authClient = RestService.For<IIdentityClient>(
            TestHelpers.GetAuthenticatedClient(loginResult.AccessToken, StorageConstants.DefaultTenantId));

        // Act
        await authClient.ChangePasswordAsync(new ChangePasswordRequest(oldPassword, newPassword));

        // Act - Try login with new password
        var nextLoginResult = await _client.LoginAsync(new LoginRequest(email, newPassword));

        // Assert
        _ = await Assert.That(nextLoginResult.AccessToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task AddPassword_WhenManualClearance_ShouldSucceed()
    {
        // Arrange
        var email = TestHelpers.GenerateFakeEmail();
        var tempPassword = TestHelpers.GenerateFakePassword();
        var newPassword = TestHelpers.GenerateFakePassword();

        // Register normally
        _ = await _client.RegisterAsync(new RegisterRequest(email, tempPassword));
        var loginResult = await _client.LoginAsync(new LoginRequest(email, tempPassword));

        var authClient = RestService.For<IIdentityClient>(
            TestHelpers.GetAuthenticatedClient(loginResult.AccessToken, StorageConstants.DefaultTenantId));

        // Manually clear password hash in DB
        using var store = await GetStoreAsync();
        await using (var session = store.LightweightSession(StorageConstants.DefaultTenantId))
        {
            var user = await session.Query<ApplicationUser>()
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            _ = await Assert.That(user).IsNotNull();
            user!.PasswordHash = null;
            session.Update(user);
            await session.SaveChangesAsync();
        }

        // Verify it reports no password
        var status = await authClient.GetPasswordStatusAsync();
        _ = await Assert.That(status.HasPassword).IsFalse();

        // Act
        await authClient.AddPasswordAsync(new AddPasswordRequest(newPassword));

        // Act - Try login with added password
        var finalLoginResult = await _client.LoginAsync(new LoginRequest(email, newPassword));

        // Assert
        _ = await Assert.That(finalLoginResult.AccessToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task ChangePassword_WithSamePassword_ShouldReturnBadRequest()
    {
        // Arrange
        var email = TestHelpers.GenerateFakeEmail();
        var password = TestHelpers.GenerateFakePassword();

        // Register
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        // Login to get token
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        var authClient = RestService.For<IIdentityClient>(
            TestHelpers.GetAuthenticatedClient(loginResult.AccessToken, StorageConstants.DefaultTenantId));

        // Act & Assert
        try
        {
            await authClient.ChangePasswordAsync(new ChangePasswordRequest(password, password));
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.BadRequest);
            var problem = await ex.GetContentAsAsync<TestHelpers.ValidationProblemDetails>();
            _ = await Assert.That(problem?.Error).IsEqualTo(ErrorCodes.Auth.PasswordReuse);
        }
    }

    [Test]
    public async Task RemovePassword_Fails_WhenUserHasNoPasskey()
    {
        // Arrange
        var email = TestHelpers.GenerateFakeEmail();
        var password = TestHelpers.GenerateFakePassword();

        // Register
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        // Login
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        var authClient = RestService.For<IIdentityClient>(
            TestHelpers.GetAuthenticatedClient(loginResult.AccessToken, StorageConstants.DefaultTenantId));

        // Act & Assert
        try
        {
            await authClient.RemovePasswordAsync(new RemovePasswordRequest());
            Assert.Fail("Should have thrown ApiException");
        }
        catch (ApiException ex)
        {
            _ = await Assert.That((int)ex.StatusCode).IsEqualTo((int)HttpStatusCode.BadRequest);
            var problem = await ex.GetContentAsAsync<TestHelpers.ValidationProblemDetails>();
            _ = await Assert.That(problem?.Error).IsEqualTo(ErrorCodes.Auth.InvalidRequest);
        }
    }

    [Test]
    public async Task RemovePassword_Succeeds_WhenUserHasPasskey()
    {
        // Arrange
        var email = TestHelpers.GenerateFakeEmail();
        var password = TestHelpers.GenerateFakePassword();

        // Register
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));

        // Login
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        var authClient = RestService.For<IIdentityClient>(
            TestHelpers.GetAuthenticatedClient(loginResult.AccessToken, StorageConstants.DefaultTenantId));

        // Manually add a passkey
        using var store = await GetStoreAsync();
        await using (var session = store.LightweightSession(StorageConstants.DefaultTenantId))
        {
            var user = await session.Query<ApplicationUser>()
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            _ = await Assert.That(user).IsNotNull();

            user!.Passkeys.Add(new UserPasskeyInfo(
                Guid.NewGuid().ToByteArray(), // credentialId
                [], // publicKey
                DateTimeOffset.UtcNow, // createdAt
                0, // signCount
                [], // transports
                true, // isUserVerified
                true, // isBackupEligible
                true, // isBackedUp
                [], // attestationObject
                [] // clientDataJson
            ));

            session.Update(user);
            await session.SaveChangesAsync();
        }

        // Act
        await authClient.RemovePasswordAsync(new RemovePasswordRequest());

        // Verify password hash is null directly in database
        // Note: RemovePasswordAsync updates the security stamp, invalidating the current token
        // This is correct security behavior - security-sensitive operations should invalidate sessions
        using var verifyStore = await GetStoreAsync();
        await using (var verifySession = verifyStore.LightweightSession(StorageConstants.DefaultTenantId))
        {
            var updatedUser = await verifySession.Query<ApplicationUser>()
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            _ = await Assert.That(updatedUser).IsNotNull();
            _ = await Assert.That(updatedUser!.PasswordHash).IsNull();
        }
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
}
