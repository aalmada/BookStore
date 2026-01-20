using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Logging;
using Microsoft.Extensions.Logging;

namespace BookStore.Web.Services;

public class PasskeyService
{
    readonly IPasskeyClient _passkeyClient;
    readonly ILogger<PasskeyService> _logger;

    public PasskeyService(IPasskeyClient passkeyClient, ILogger<PasskeyService> logger)
    {
        _passkeyClient = passkeyClient;
        _logger = logger;
    }

    public async Task<(string? Options, string? Error)> GetCreationOptionsAsync(string? email = null)
    {
        try
        {
            var response =
                await _passkeyClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest { Email = email });
            var optionsJson = System.Text.Json.JsonSerializer.Serialize(response.Options);
            return (optionsJson, null);
        }
        catch (Exception ex)
        {
            Log.RegistrationOptionsError(_logger, ex);
            return (null, $"Error: {ex.Message}");
        }
    }

    public async Task<string?> GetLoginOptionsAsync(string? email = null)
    {
        try
        {
            var response =
                await _passkeyClient.GetPasskeyLoginOptionsAsync(new PasskeyLoginOptionsRequest { Email = email });
            return System.Text.Json.JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            Log.LoginOptionsError(_logger, ex);
        }

        return null;
    }

    public async Task<LoginResult?> RegisterPasskeyAsync(string credentialJson, string? email = null,
        string? userId = null)
    {
        try
        {
            await _passkeyClient.RegisterPasskeyAsync(new RegisterPasskeyRequest
            {
                CredentialJson = credentialJson,
                Email = email,
                UserId = userId
            });

            // If no exception, it succeeded.
            return new LoginResult(true, null, null, null);
        }
        catch (Exception ex)
        {
            Log.RegistrationCompleteError(_logger, ex);
            return new LoginResult(false, ex.Message, null, null);
        }
    }

    public async Task<LoginResult?> LoginWithPasskeyAsync(string credentialJson)
    {
        try
        {
            var loginResponse = await _passkeyClient.LoginPasskeyAsync(new RegisterPasskeyRequest
            {
                CredentialJson = credentialJson
            });

            if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                return new LoginResult(true, null, loginResponse.AccessToken, loginResponse.RefreshToken);
            }

            return new LoginResult(false, "Passkey login failed.", null, null);
        }
        catch (Exception ex)
        {
            Log.LoginCompleteError(_logger, ex);
            return new LoginResult(false, ex.Message, null, null);
        }
    }
}
