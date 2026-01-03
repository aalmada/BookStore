using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.Web.Services;

public class PasskeyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PasskeyService> _logger;

    public PasskeyService(HttpClient httpClient, ILogger<PasskeyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(string? Options, string? Error)> GetCreationOptionsAsync(string? email = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Account/PasskeyCreationOptions", new { Email = email });
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadAsStringAsync(), null);
            }
            
            var error = await response.Content.ReadAsStringAsync();
            // Try to make it cleaner if it's JSON
            if (error.StartsWith("\"") && error.EndsWith("\"")) error = error.Trim('"');
            
            _logger.LogWarning("Failed to get creation options: {StatusCode} {Error}", response.StatusCode, error);
            
            return (null, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passkey creation options");
            return (null, "An internal error occurred.");
        }
    }

    public async Task<string?> GetLoginOptionsAsync(string? email = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Account/PasskeyLoginOptions", new { Email = email });
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            _logger.LogWarning("Failed to get login options: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passkey login options");
        }
        return null;
    }

    public async Task<LoginResult?> RegisterPasskeyAsync(string credentialJson, string? email = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Account/RegisterPasskey", new { CredentialJson = credentialJson, Email = email });
            
            if (email != null && response.IsSuccessStatusCode)
            {
                 // Registration mode returns tokens
                 return await response.Content.ReadFromJsonAsync<LoginResult>();
            }
            
            return new LoginResult(response.IsSuccessStatusCode, response.ReasonPhrase, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering passkey");
            return new LoginResult(false, ex.Message, null, null);
        }
    }

    public async Task<LoginResponse?> LoginPasskeyAsync(string credentialJson)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("Account/LoginPasskey", new { CredentialJson = credentialJson });
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginResponse>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging in with passkey");
        }
        return null;
    }
}
