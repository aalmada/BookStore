using System.Threading;
using System.Threading.Tasks;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetPasskeyCreationOptionsEndpoint
{
    /// <summary>
    /// Get Passkey creation options (attestation challenge)
    /// </summary>
    [Post("/account/attestation/options")]
    Task<PasskeyCreationOptionsResponse> GetPasskeyCreationOptionsAsync(
        [Body] PasskeyCreationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRegisterPasskeyEndpoint
{
    /// <summary>
    /// Register a Passkey (finish registration)
    /// </summary>
    [Post("/account/attestation/result")]
    Task RegisterPasskeyAsync(
        [Body] RegisterPasskeyRequest request,
        CancellationToken cancellationToken = default);
}

public interface IGetPasskeyLoginOptionsEndpoint
{
    /// <summary>
    /// Get Passkey login options (assertion challenge)
    /// </summary>
    [Post("/account/assertion/options")]
    Task<object> GetPasskeyLoginOptionsAsync(
        [Body] PasskeyLoginOptionsRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILoginPasskeyEndpoint
{
    /// <summary>
    /// Login with a Passkey
    /// </summary>
    [Post("/account/assertion/result")]
    Task<LoginResponse> LoginPasskeyAsync(
        [Body] RegisterPasskeyRequest request,
        CancellationToken cancellationToken = default);
}

public interface IListPasskeysEndpoint
{
    /// <summary>
    /// List all passkeys for the current user
    /// </summary>
    [Get("/account/passkeys")]
    Task<IReadOnlyList<PasskeyInfo>> ListPasskeysAsync(
        CancellationToken cancellationToken = default);
}

    public interface IDeletePasskeyEndpoint
    {
        /// <summary>
        /// Delete a passkey by ID
        /// </summary>
        [Delete("/account/passkeys/{id}")]
        Task DeletePasskeyAsync(
            string id,
            [Header("If-Match")] string? etag = null,
            CancellationToken cancellationToken = default);
    }
