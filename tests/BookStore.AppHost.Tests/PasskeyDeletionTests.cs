using Bogus;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Identity;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class PasskeyDeletionTests
{
    readonly IIdentityClient _client;
    readonly IPasskeyClient _passkeyClient;
    readonly Faker _faker;

    public PasskeyDeletionTests()
    {
        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        _client = RestService.For<IIdentityClient>(httpClient);
        _passkeyClient = RestService.For<IPasskeyClient>(httpClient);
        _faker = new Faker();
    }

    [Test]
    public async Task DeletePasskey_WithUrlUnsafeId_ShouldSucceed()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = "Password123!";

        // 1. Register and login to get token
        _ = await _client.RegisterAsync(new RegisterRequest(email, password));
        var loginResult = await _client.LoginAsync(new LoginRequest(email, password));

        var authClient = HttpClientHelpers.GetAuthenticatedClient(loginResult.AccessToken);
        var authenticatedPasskeyClient = RestService.For<IPasskeyClient>(authClient);

        // 2. Manually seed a passkey with an ID that is NOT URL-safe in standard Base64
        // Base64 of [62, 62, 62, 63, 63, 63] is "+++/////"
        var unsafeCredentialId = new byte[] { 62, 62, 62, 63, 63, 63 };

        await PasskeyTestHelpers.AddPasskeyToUserAsync(
            StorageConstants.DefaultTenantId,
            email,
            "Unsafe Passkey",
            unsafeCredentialId);

        // 3. List passkeys to get the encoded ID
        var passkeys = await authenticatedPasskeyClient.ListPasskeysAsync();

        _ = await Assert.That(passkeys).IsNotNull();
        var targetPasskey = passkeys!.FirstOrDefault(p => p.Name == "Unsafe Passkey");
        _ = await Assert.That(targetPasskey).IsNotNull();

        // The ID should be Base64Url encoded now
        var encodedId = targetPasskey!.Id;
        _ = await Assert.That(encodedId).DoesNotContain("/");
        _ = await Assert.That(encodedId).DoesNotContain("+");

        // 4. Act - Delete the passkey
        await authenticatedPasskeyClient.DeletePasskeyAsync(encodedId, "\"0\"");

        // 5. Assert

        // Verify it's gone from DB
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(StorageConstants.DefaultTenantId);
        var user = await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

        _ = await Assert.That(user!.Passkeys.Any(p => p.CredentialId.SequenceEqual(unsafeCredentialId))).IsFalse();
    }
}
