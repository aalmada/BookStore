using BookStore.ApiService.Models;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Identity;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Shared helper methods for passkey-related tests.
/// Centralizes common operations to avoid code duplication across test files.
/// </summary>
public static class PasskeyTestHelpers
{
    /// <summary>
    /// Creates a UserPasskeyInfo instance for testing purposes.
    /// </summary>
    public static UserPasskeyInfo CreatePasskeyInfo(
        byte[] credentialId,
        string name,
        uint signCount = 0,
        DateTimeOffset? createdAt = null) => new(
            credentialId,
            [], // publicKey
            createdAt ?? DateTimeOffset.UtcNow,
            signCount,
            [], // transports
            true, // isUserVerified
            false, // isBackupEligible
            false, // isBackedUp
            [], // attestationObject
            [] // clientDataJson
        )
        {
            Name = name
        };

    /// <summary>
    /// Adds a passkey to a user in the database.
    /// </summary>
    public static async Task AddPasskeyToUserAsync(
        string tenantId,
        string email,
        string name,
        byte[] credentialId,
        uint signCount = 0)
    {
        var store = await TestHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);

        var user = await TestHelpers.GetUserByEmailAsync(session, email);
        if (user == null)
        {
            throw new InvalidOperationException($"User not found: {email}");
        }

        var passkey = CreatePasskeyInfo(credentialId, name, signCount);
        user.Passkeys.Add(passkey);

        session.Update(user);
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the sign count of an existing passkey.
    /// </summary>
    public static async Task UpdatePasskeySignCountAsync(
        string tenantId,
        string email,
        byte[] credentialId,
        uint signCount)
    {
        var store = await TestHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);

        var user = await TestHelpers.GetUserByEmailAsync(session, email);
        if (user == null)
        {
            throw new InvalidOperationException($"User not found: {email}");
        }

        var passkey = user.Passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId));
        if (passkey == null)
        {
            throw new InvalidOperationException("Passkey not found");
        }

        // Replace the passkey with updated sign count (UserPasskeyInfo is immutable)
        _ = user.Passkeys.Remove(passkey);
        var updatedPasskey = new UserPasskeyInfo(
            passkey.CredentialId,
            passkey.PublicKey,
            passkey.CreatedAt,
            signCount, // new value
            passkey.Transports,
            passkey.IsUserVerified,
            passkey.IsBackupEligible,
            passkey.IsBackedUp,
            passkey.AttestationObject,
            passkey.ClientDataJson
        )
        {
            Name = passkey.Name
        };
        user.Passkeys.Add(updatedPasskey);

        session.Update(user);
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a passkey using reflection for legacy code compatibility.
    /// This is a fallback for tests that need to work with different UserPasskeyInfo versions.
    /// </summary>
    [Obsolete("Use CreatePasskeyInfo instead. This method is only for backward compatibility.")]
    public static object CreatePasskeyViaReflection(byte[] credentialId, string name)
    {
        var passkeyType = typeof(UserPasskeyInfo);
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
