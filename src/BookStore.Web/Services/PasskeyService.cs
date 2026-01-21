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
            // Return the entire response (with both 'options' and 'userId')
            // The frontend needs the userId to pass back during registration
            var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
            return (responseJson, null);
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
        catch (Refit.ApiException ex)
        {
            var errorMessage = ParseError(ex.Content);
            return new LoginResult(false, errorMessage, null, null);
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
        catch (Refit.ApiException ex)
        {
            var errorMessage = ParseError(ex.Content);
            return new LoginResult(false, errorMessage, null, null);
        }
        catch (Exception ex)
        {
            Log.LoginCompleteError(_logger, ex);
            return new LoginResult(false, ex.Message, null, null);
        }
    }

    static string ParseError(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "Operation failed";
        }

        try
        {
            // Try to parse standard { "errors": [ { "description": "..." } ] }
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var error in errors.EnumerateArray())
                    {
                        if (error.TryGetProperty("description", out var desc))
                        {
                            return desc.GetString() ?? "Unknown error";
                        }
                    }
                }

                // Try to parse standard ProblemDetails "detail"
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "Operation failed";
                }
            }
            // If it's a direct array [ { "description": "..." } ]
            else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var error in doc.RootElement.EnumerateArray())
                {
                    if (error.TryGetProperty("description", out var desc))
                    {
                        return desc.GetString() ?? "Unknown error";
                    }
                }
            }
        }
        catch
        {
            // Fallback to raw content if not JSON
        }

        // Clean up quotes if it's a simple string
        if (content.StartsWith("\"") && content.EndsWith("\""))
        {
            return content.Trim('"');
        }

        return content;
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

    public async Task<bool> DeletePasskeyAsync(string id)
    {
        try
        {
            await _passkeyClient.DeletePasskeyAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            Log.RegistrationCompleteError(_logger, ex);
            return false;
        }
    }
}
