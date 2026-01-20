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
