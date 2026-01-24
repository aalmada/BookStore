using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Infrastructure;
using BookStore.Web.Logging;
using Microsoft.Extensions.Logging;
using Refit;

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

    public async Task<Result<string>> GetCreationOptionsAsync(string? email = null)
    {
        try
        {
            var response =
                await _passkeyClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest { Email = email });
            // Return the entire response (with both 'options' and 'userId')
            // The frontend needs the userId to pass back during registration
            var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
            return Result.Success(responseJson);
        }
        catch (ApiException ex)
        {
            Log.RegistrationOptionsError(_logger, ex);
            return ex.ToResult<string>();
        }
        catch (Exception ex)
        {
            Log.RegistrationOptionsError(_logger, ex);
            return Result.Failure<string>(Error.Failure("ERR_PASSKEY_OPTIONS_FAILED", ex.Message));
        }
    }

    public async Task<Result<string>> GetLoginOptionsAsync(string? email = null)
    {
        try
        {
            var response =
                await _passkeyClient.GetPasskeyLoginOptionsAsync(new PasskeyLoginOptionsRequest { Email = email });
            return Result.Success(System.Text.Json.JsonSerializer.Serialize(response));
        }
        catch (ApiException ex)
        {
            Log.LoginOptionsError(_logger, ex);
            return ex.ToResult<string>();
        }
        catch (Exception ex)
        {
            Log.LoginOptionsError(_logger, ex);
            return Result.Failure<string>(Error.Failure("ERR_PASSKEY_LOGIN_OPTIONS_FAILED", ex.Message));
        }
    }

    public async Task<Result> RegisterPasskeyAsync(string credentialJson, string? email = null,
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

            return Result.Success();
        }
        catch (ApiException ex)
        {
            Log.RegistrationResultFailed(_logger, ex.StatusCode, ex.Content ?? ex.Message);
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            Log.RegistrationCompleteError(_logger, ex);
            return Result.Failure(Error.Failure("ERR_PASSKEY_REGISTRATION_FAILED", ex.Message));
        }
    }

    public async Task<Result<LoginResponse>> LoginWithPasskeyAsync(string credentialJson)
    {
        try
        {
            var loginResponse = await _passkeyClient.LoginPasskeyAsync(new RegisterPasskeyRequest
            {
                CredentialJson = credentialJson
            });

            if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                return Result.Success(loginResponse);
            }

            return Result.Failure<LoginResponse>(Error.Failure("ERR_PASSKEY_LOGIN_FAILED", "Passkey login failed."));
        }
        catch (ApiException ex)
        {
            return ex.ToResult<LoginResponse>();
        }
        catch (Exception ex)
        {
            Log.LoginCompleteError(_logger, ex);
            return Result.Failure<LoginResponse>(Error.Failure("ERR_PASSKEY_LOGIN_FAILED", ex.Message));
        }
    }

    public async Task<IReadOnlyList<PasskeyInfo>> ListPasskeysAsync()
    {
        try
        {
            return await _passkeyClient.ListPasskeysAsync();
        }
        catch (Exception ex)
        {
            Log.LoginOptionsError(_logger, ex);
            return [];
        }
    }

    public async Task<Result> DeletePasskeyAsync(string id)
    {
        try
        {
            await _passkeyClient.DeletePasskeyAsync(id);
            return Result.Success();
        }
        catch (ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            Log.RegistrationCompleteError(_logger, ex);
            return Result.Failure(Error.Failure("ERR_PASSKEY_DELETE_FAILED", ex.Message));
        }
    }
}
