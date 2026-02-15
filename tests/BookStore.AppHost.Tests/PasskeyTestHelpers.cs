using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
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
        var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);

        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
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
        var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);

        var user = await DatabaseHelpers.GetUserByEmailAsync(session, email);
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
}
