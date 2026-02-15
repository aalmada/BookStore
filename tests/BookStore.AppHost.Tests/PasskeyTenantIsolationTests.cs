using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using BookStore.ApiService.Models;
using BookStore.Client;
using Marten;
using Refit;
using Weasel.Core;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class PasskeyTenantIsolationTests
{
    [Test]
    public async Task Passkeys_AreTenantScoped()
    {
        // Arrange
        var (acmeEmail, _, acmeLoginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync("acme");
        var (_, _, contosoLoginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync("contoso");

        var credentialId = Guid.CreateVersion7().ToByteArray();
        await PasskeyTestHelpers.AddPasskeyToUserAsync("acme", acmeEmail, "Acme Passkey", credentialId);

        var acmeClient = RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(acmeLoginResponse.AccessToken, "acme"));
        var contosoClient =
            RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(contosoLoginResponse.AccessToken, "contoso"));

        // Act
        var acmePasskeys = await acmeClient.ListPasskeysAsync();
        var contosoPasskeys = await contosoClient.ListPasskeysAsync();

        // Assert
        _ = await Assert.That(acmePasskeys.Any(p => p.Name == "Acme Passkey")).IsTrue();
        _ = await Assert.That(contosoPasskeys.Any(p => p.Name == "Acme Passkey")).IsFalse();

        var mismatchedClient =
            RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(acmeLoginResponse.AccessToken, "contoso"));
        var mismatchException =
            await Assert.That(async () => await mismatchedClient.ListPasskeysAsync()).Throws<ApiException>();
        var isRejected = mismatchException!.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task DeletePasskey_WithMismatchedTenantHeader_IsRejected()
    {
        // Arrange
        var (acmeEmail, _, acmeLoginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync("acme");
        var credentialId = Guid.CreateVersion7().ToByteArray();
        await PasskeyTestHelpers.AddPasskeyToUserAsync("acme", acmeEmail, "Acme Passkey", credentialId);

        var acmeClient = RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(acmeLoginResponse.AccessToken, "acme"));
        var passkeys = await acmeClient.ListPasskeysAsync();
        var passkeyId = passkeys.Single(p => p.Name == "Acme Passkey").Id;

        var mismatchedClient =
            RestService.For<IPasskeyClient>(HttpClientHelpers.GetAuthenticatedClient(acmeLoginResponse.AccessToken, "contoso"));

        // Act
        var mismatchException = await Assert.That(
            async () => await mismatchedClient.DeletePasskeyAsync(passkeyId, "\"0\""))
            .Throws<ApiException>();

        // Assert
        var isRejected = mismatchException!.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();

        var remaining = await acmeClient.ListPasskeysAsync();
        _ = await Assert.That(remaining.Any(p => p.Name == "Acme Passkey")).IsTrue();
    }

    [Test]
    public async Task PasskeyCreationOptions_WithEmailFromOtherTenant_ReturnsFreshUserId()
    {
        // Arrange
        var (acmeEmail, _, acmeLoginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync("acme");
        var contosoClient = RestService.For<IPasskeyClient>(HttpClientHelpers.GetUnauthenticatedClient("contoso"));

        // Get acme user ID from JWT token
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(acmeLoginResponse.AccessToken);
        var acmeUserId = Guid.Parse(token.Claims.First(c => c.Type == "sub").Value);

        // Act
        var response = await contosoClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest
        {
            Email = acmeEmail
        });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Options).IsNotNull();
        _ = await Assert.That(Guid.TryParse(response.UserId, out var contosoUserId)).IsTrue();
        _ = await Assert.That(contosoUserId).IsNotEqualTo(acmeUserId);
    }
}
