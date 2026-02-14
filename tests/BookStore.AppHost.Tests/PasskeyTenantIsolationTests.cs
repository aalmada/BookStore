using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using BookStore.ApiService.Models;
using BookStore.Client;
using Marten;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class PasskeyTenantIsolationTests
{
    [Test]
    public async Task Passkeys_AreTenantScoped()
    {
        // Arrange
        var acme = await RegisterAndLoginAsync("acme");
        var contoso = await RegisterAndLoginAsync("contoso");

        var credentialId = Guid.CreateVersion7().ToByteArray();
        await AddPasskeyAsync("acme", acme.Email, "Acme Passkey", credentialId);

        var acmeClient = RestService.For<IPasskeyClient>(TestHelpers.GetAuthenticatedClient(acme.AccessToken, "acme"));
        var contosoClient =
            RestService.For<IPasskeyClient>(TestHelpers.GetAuthenticatedClient(contoso.AccessToken, "contoso"));

        // Act
        var acmePasskeys = await acmeClient.ListPasskeysAsync();
        var contosoPasskeys = await contosoClient.ListPasskeysAsync();

        // Assert
        _ = await Assert.That(acmePasskeys.Any(p => p.Name == "Acme Passkey")).IsTrue();
        _ = await Assert.That(contosoPasskeys.Any(p => p.Name == "Acme Passkey")).IsFalse();

        var mismatchedClient =
            RestService.For<IPasskeyClient>(TestHelpers.GetAuthenticatedClient(acme.AccessToken, "contoso"));
        var mismatchException =
            await Assert.That(async () => await mismatchedClient.ListPasskeysAsync()).Throws<ApiException>();
        var isRejected = mismatchException!.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task DeletePasskey_WithMismatchedTenantHeader_IsRejected()
    {
        // Arrange
        var acme = await RegisterAndLoginAsync("acme");
        var credentialId = Guid.CreateVersion7().ToByteArray();
        await AddPasskeyAsync("acme", acme.Email, "Acme Passkey", credentialId);

        var acmeClient = RestService.For<IPasskeyClient>(TestHelpers.GetAuthenticatedClient(acme.AccessToken, "acme"));
        var passkeys = await acmeClient.ListPasskeysAsync();
        var passkeyId = passkeys.Single(p => p.Name == "Acme Passkey").Id;

        var mismatchedClient =
            RestService.For<IPasskeyClient>(TestHelpers.GetAuthenticatedClient(acme.AccessToken, "contoso"));

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
        var acme = await RegisterAndLoginAsync("acme");
        var contosoClient = RestService.For<IPasskeyClient>(TestHelpers.GetUnauthenticatedClient("contoso"));

        // Act
        var response = await contosoClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest
        {
            Email = acme.Email
        });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Options).IsNotNull();
        _ = await Assert.That(Guid.TryParse(response.UserId, out var contosoUserId)).IsTrue();
        _ = await Assert.That(contosoUserId).IsNotEqualTo(acme.UserId);
    }

    static async Task<(string Email, string AccessToken, Guid UserId)> RegisterAndLoginAsync(string tenantId)
    {
        var email = TestHelpers.GenerateFakeEmail();
        var password = TestHelpers.GenerateFakePassword();

        var client = TestHelpers.GetUnauthenticatedClient(tenantId);
        var registerResponse = await client.PostAsJsonAsync("/account/register", new { email, password });
        _ = registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/account/login", new { email, password });
        _ = loginResponse.EnsureSuccessStatusCode();

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();
        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Login response was null.");
        }

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenResponse.AccessToken);
        var userId = Guid.Parse(token.Claims.First(c => c.Type == "sub").Value);

        return (email, tokenResponse.AccessToken, userId);
    }

    static async Task AddPasskeyAsync(string tenantId, string email, string name, byte[] credentialId)
    {
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await using var session = store.LightweightSession(tenantId);
        var user = await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

        if (user == null)
        {
            throw new InvalidOperationException($"User not found for tenant '{tenantId}'.");
        }

        var passkey = CreatePasskey(user, credentialId, name);
        var passkeyList = (System.Collections.IList)user.Passkeys;
        passkeyList.Add(passkey);
        session.Update(user);
        await session.SaveChangesAsync();
    }

    static object CreatePasskey(ApplicationUser user, byte[] credentialId, string name)
    {
        var passkeyType = user.Passkeys.GetType().GetGenericArguments()[0];
        var constructors = passkeyType.GetConstructors(System.Reflection.BindingFlags.Instance |
                                                       System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic);
        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType == typeof(byte[]))
            {
                args[i] = Array.Empty<byte>();
            }
            else if (parameter.ParameterType == typeof(DateTimeOffset))
            {
                args[i] = DateTimeOffset.UtcNow;
            }
            else if (parameter.ParameterType == typeof(uint))
            {
                args[i] = 0u;
            }
            else if (parameter.ParameterType == typeof(bool))
            {
                args[i] = false;
            }
            else
            {
                args[i] = null;
            }
        }

        var passkey = constructor.Invoke(args) ?? throw new InvalidOperationException("Failed to create passkey.");

        var fields = passkeyType.GetFields(System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.NonPublic |
                           System.Reflection.BindingFlags.Public);

        var credentialIdField = fields.FirstOrDefault(f =>
            f.Name.Contains("<CredentialId>k__BackingField") || f.Name == "_credentialId" || f.Name == "credentialId");
        credentialIdField?.SetValue(passkey, credentialId);

        var nameField = fields.FirstOrDefault(f =>
            f.Name.Contains("<Name>k__BackingField") || f.Name == "_name" || f.Name == "name");
        nameField?.SetValue(passkey, name);

        return passkey;
    }
}
