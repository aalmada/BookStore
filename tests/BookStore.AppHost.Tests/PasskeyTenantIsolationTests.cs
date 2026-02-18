using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Marten;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class PasskeyTenantIsolationTests
{
    [Test]
    public async Task Passkeys_AreTenantScoped()
    {
        // Arrange: two fresh isolated tenants
        var tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        var tenant2 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant1);
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant2);

        var (email1, password1, _, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenant1);
        var (_, _, tenant2LoginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenant2);

        var credentialId = Guid.CreateVersion7().ToByteArray();
        const string passkeyName = "My Passkey";
        await PasskeyTestHelpers.AddPasskeyToUserAsync(tenant1, email1, passkeyName, credentialId);

        // Get fresh token after adding passkey (security stamp changed)
        var tenant1IdentityClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenant1));
        var tenant1RefreshedResponse = await tenant1IdentityClient.LoginAsync(new LoginRequest(email1, password1));

        var tenant1Client = RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(tenant1RefreshedResponse.AccessToken, tenant1));
        var tenant2Client = RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(tenant2LoginResponse.AccessToken, tenant2));

        // Act
        var tenant1Passkeys = await tenant1Client.ListPasskeysAsync();
        var tenant2Passkeys = await tenant2Client.ListPasskeysAsync();

        // Assert: passkey is visible only in the tenant it was created in
        _ = await Assert.That(tenant1Passkeys.Any(p => p.Name == passkeyName)).IsTrue();
        _ = await Assert.That(tenant2Passkeys.Any(p => p.Name == passkeyName)).IsFalse();

        // Assert: using tenant1 token against tenant2 header is rejected
        var mismatchedClient = RestService.For<IPasskeyClient>(
            HttpClientHelpers.GetAuthenticatedClient(tenant1RefreshedResponse.AccessToken, tenant2));
        var mismatchException =
            await Assert.That(async () => await mismatchedClient.ListPasskeysAsync()).Throws<ApiException>();
        var isRejected = mismatchException!.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task DeletePasskey_WithMismatchedTenantHeader_IsRejected()
    {
        // Arrange: a fresh tenant for the real user, and a second tenant whose header will be spoofed
        var tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        var tenant2 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant1);
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant2);

        var (email, password, _, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenant1);
        var credentialId = Guid.CreateVersion7().ToByteArray();
        const string passkeyName = "My Passkey";
        await PasskeyTestHelpers.AddPasskeyToUserAsync(tenant1, email, passkeyName, credentialId);

        // Get fresh token after adding passkey (security stamp changed)
        var identityClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenant1));
        var refreshedResponse = await identityClient.LoginAsync(new LoginRequest(email, password));

        var tenant1Client = RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(refreshedResponse.AccessToken, tenant1));
        var passkeys = await tenant1Client.ListPasskeysAsync();
        var passkeyId = passkeys.Single(p => p.Name == passkeyName).Id;

        // Use tenant1 access token but send tenant2 header (cross-tenant attack)
        var mismatchedClient = RestService.For<IPasskeyClient>(
            HttpClientHelpers.GetAuthenticatedClient(refreshedResponse.AccessToken, tenant2));

        // Act
        var mismatchException = await Assert.That(
            async () => await mismatchedClient.DeletePasskeyAsync(passkeyId, "\"0\""))
            .Throws<ApiException>();

        // Assert: request is rejected
        var isRejected = mismatchException!.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();

        // Assert: passkey still exists in the correct tenant
        var remaining = await tenant1Client.ListPasskeysAsync();
        _ = await Assert.That(remaining.Any(p => p.Name == passkeyName)).IsTrue();
    }

    [Test]
    public async Task PasskeyCreationOptions_WithEmailFromOtherTenant_ReturnsFreshUserId()
    {
        // Arrange: two fresh isolated tenants sharing the same email
        var tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        var tenant2 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant1);
        await DatabaseHelpers.CreateTenantViaApiAsync(tenant2);

        var (sharedEmail, _, tenant1LoginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(tenant1);
        var tenant2PasskeyClient = RestService.For<IPasskeyClient>(HttpClientHelpers.GetUnauthenticatedClient(tenant2));

        // Get tenant1 user ID from JWT
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tenant1LoginResponse.AccessToken);
        var tenant1UserId = Guid.Parse(token.Claims.First(c => c.Type == "sub").Value);

        // Act: request passkey creation on tenant2 using the same email
        var response = await tenant2PasskeyClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest
        {
            Email = sharedEmail
        });

        // Assert: tenant2 assigns a brand-new user ID (not the same as tenant1's)
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Options).IsNotNull();
        _ = await Assert.That(Guid.TryParse(response.UserId, out var tenant2UserId)).IsTrue();
        _ = await Assert.That(tenant2UserId).IsNotEqualTo(tenant1UserId);
    }
}
