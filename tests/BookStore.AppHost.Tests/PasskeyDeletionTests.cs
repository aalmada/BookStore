using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using JasperFx;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Identity;
using TUnit.Core.Interfaces;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class PasskeyDeletionTests
{
    // Define local record to match API response
    record PasskeyInfo(string Id, string Name, DateTimeOffset? CreatedAt);

    readonly HttpClient _client;
    readonly Faker _faker;

    public PasskeyDeletionTests()
    {
        _client = TestHelpers.GetUnauthenticatedClient();
        _faker = new Faker();
    }

    [Test]
    public async Task DeletePasskey_WithUrlUnsafeId_ShouldSucceed()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = "Password123!";

        // 1. Register and login to get token
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TestHelpers.LoginResponse>();

        var authClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        authClient.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

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
        var listResponse = await authClient.GetAsync("/account/passkeys");
        _ = await Assert.That(listResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var passkeys = await listResponse.Content.ReadFromJsonAsync<List<PasskeyInfo>>();

        _ = await Assert.That(passkeys).IsNotNull();
        var targetPasskey = passkeys!.FirstOrDefault(p => p.Name == "Unsafe Passkey");
        _ = await Assert.That(targetPasskey).IsNotNull();

        // The ID should be Base64Url encoded now
        var encodedId = targetPasskey!.Id;
        _ = await Assert.That(encodedId).DoesNotContain("/");
        _ = await Assert.That(encodedId).DoesNotContain("+");

        // 4. Act - Delete the passkey
        var deleteResponse = await authClient.DeleteAsync($"/account/passkeys/{encodedId}");

        // 5. Assert
        _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

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
