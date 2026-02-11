using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IPasskeyClient
{
    /// <summary>
    /// Get Passkey creation options (attestation challenge)
    /// </summary>
    [Post("/account/attestation/options")]
    Task<PasskeyCreationOptionsResponse> GetPasskeyCreationOptionsAsync(
        [Body] PasskeyCreationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a Passkey (finish registration)
    /// </summary>
    [Post("/account/attestation/result")]
    Task RegisterPasskeyAsync(
        [Body] RegisterPasskeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Passkey login options (assertion challenge)
    /// </summary>
    [Post("/account/assertion/options")]
    Task<object> GetPasskeyLoginOptionsAsync(
        [Body] PasskeyLoginOptionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Login with a Passkey
    /// </summary>
    [Post("/account/assertion/result")]
    Task<LoginResponse> LoginPasskeyAsync(
        [Body] RegisterPasskeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all passkeys for the current user
    /// </summary>
    [Get("/account/passkeys")]
    Task<IReadOnlyList<PasskeyInfo>> ListPasskeysAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a passkey by ID
    /// </summary>
    [Delete("/account/passkeys/{id}")]
    Task DeletePasskeyAsync(
        string id,
        [Header("If-Match")] string? etag = null,
        CancellationToken cancellationToken = default);
}
