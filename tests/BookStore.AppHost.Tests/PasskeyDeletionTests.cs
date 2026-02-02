using Bogus;
using BookStore.ApiService.Models;
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
        var httpClient = TestHelpers.GetUnauthenticatedClient();
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

        var authClient = TestHelpers.GetAuthenticatedClient(loginResult.AccessToken);
        var authenticatedPasskeyClient = RestService.For<IPasskeyClient>(authClient);

        // 2. Manually seed a passkey with an ID that is NOT URL-safe in standard Base64
        // Base64 of [62, 62, 62, 63, 63, 63] is "+++/////"
        var unsafeCredentialId = new byte[] { 62, 62, 62, 63, 63, 63 };

        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await using (var session = store.LightweightSession(StorageConstants.DefaultTenantId))
        {
            var user = await session.Query<ApplicationUser>()
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            _ = await Assert.That(user).IsNotNull();

            // Add the unsafe passkey
            // Use reflection because UserPasskeyInfo has a complex constructor and read-only properties
            var constructors = typeof(UserPasskeyInfo).GetConstructors(System.Reflection.BindingFlags.Instance |
                                                                       System.Reflection.BindingFlags.Public |
                                                                       System.Reflection.BindingFlags.NonPublic);
            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var args = new object?[parameters.Length];

            // Try to find reasonable defaults for arguments
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(byte[]))
                {
                    args[i] = Array.Empty<byte>();
                }
                else if (p.ParameterType == typeof(DateTimeOffset))
                {
                    args[i] = DateTimeOffset.UtcNow;
                }
                else if (p.ParameterType == typeof(uint))
                {
                    args[i] = 0u;
                }
                else if (p.ParameterType == typeof(bool))
                {
                    args[i] = false;
                }
                else
                {
                    args[i] = null;
                }
            }

            var passkey = (UserPasskeyInfo)constructor.Invoke(args);

            var allFields = typeof(UserPasskeyInfo).GetFields(System.Reflection.BindingFlags.Instance |
                                                              System.Reflection.BindingFlags.NonPublic |
                                                              System.Reflection.BindingFlags.Public);

            var credentialIdField = allFields.FirstOrDefault(f =>
                f.Name.Contains("<CredentialId>k__BackingField") || f.Name == "_credentialId" ||
                f.Name == "credentialId");
            if (credentialIdField != null)
            {
                credentialIdField.SetValue(passkey, unsafeCredentialId);
            }
            else
            {
                // Fallback: try setting property even if it failed before, or maybe it has a private setter
                var prop = typeof(UserPasskeyInfo).GetProperty("CredentialId");
                if (prop != null)
                {
                    prop.SetValue(passkey, unsafeCredentialId);
                }
            }

            var nameField = allFields.FirstOrDefault(f =>
                f.Name.Contains("<Name>k__BackingField") || f.Name == "_name" || f.Name == "name");
            if (nameField != null)
            {
                nameField.SetValue(passkey, "Unsafe Passkey");
            }
            else
            {
                var prop = typeof(UserPasskeyInfo).GetProperty("Name");
                if (prop != null)
                {
                    prop.SetValue(passkey, "Unsafe Passkey");
                }
            }

            user!.Passkeys.Add(passkey);

            session.Update(user);
            await session.SaveChangesAsync();
        }

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
        await authenticatedPasskeyClient.DeletePasskeyAsync(encodedId);

        // 5. Assert

        // Verify it's gone from DB
        await using (var session = store.LightweightSession(StorageConstants.DefaultTenantId))
        {
            var user = await session.Query<ApplicationUser>()
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            _ = await Assert.That(user!.Passkeys.Any(p => p.CredentialId.SequenceEqual(unsafeCredentialId))).IsFalse();
        }
    }
}
