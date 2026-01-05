using System.Net.Http.Json;
using System.Text.Json;
using BookStore.Shared.Models;
using Microsoft.Extensions.Logging;

namespace BookStore.Web.Services;

public class PasskeyService
{
    readonly HttpClient _httpClient;
    readonly ILogger<PasskeyService> _logger;

    public PasskeyService(HttpClient httpClient, ILogger<PasskeyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(string? Options, string? Error)> GetCreationOptionsAsync(string? email = null)
    {
        try
        {
            // Use standard lowercase path
            var response = await _httpClient.PostAsJsonAsync("account/attestation/options", new { Email = email });
            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadAsStringAsync(), null);
            }

            var error = await response.Content.ReadAsStringAsync();
            var errorMessage = ParseError(error);
            _logger.LogWarning("Failed to get creation options: {StatusCode} {Error}", response.StatusCode, errorMessage);

            return (null, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passkey creation options");
            // Return full exception to debug connection issues
            return (null, $"Internal Error: {ex}");
        }
    }

    public async Task<string?> GetLoginOptionsAsync(string? email = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("account/assertion/options", new { Email = email });
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

    public async Task<LoginResult?> RegisterPasskeyAsync(string credentialJson, string? email = null, string? userId = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("account/attestation/result", new
            {
                CredentialJson = credentialJson,
                Email = email,
                UserId = userId
            });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                // Try to parse as LoginResponse first
                try
                {
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content);
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
                    {
                        return new LoginResult(true, null, loginResponse.AccessToken, loginResponse.RefreshToken);
                    }
                }
                catch
                {
                    // Not a login response, might be a verification message
                }

                // If no tokens, it means email verification is required
                return new LoginResult(true, null, null, null);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Passkey registration failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return new LoginResult(false, $"Passkey registration failed: {errorContent}", null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering passkey");
            return new LoginResult(false, ex.Message, null, null);
        }
    }

    public async Task<LoginResult?> LoginWithPasskeyAsync(string credentialJson)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("account/assertion/result", new
            {
                CredentialJson = credentialJson
            });

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                return new LoginResult(true, null, loginResponse?.AccessToken, loginResponse?.RefreshToken);
            }

            // Return error from server
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Passkey login failed: {StatusCode} - {Error}", response.StatusCode, errorContent);

            return new LoginResult(false, errorContent, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during passkey login");
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
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var error in errors.EnumerateArray())
                    {
                        if (error.TryGetProperty("description", out var desc))
                        {
                            return desc.GetString() ?? "Unknown error";
                        }
                    }
                }

                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "Operation failed";
                }
            }
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
            // Logic for simple string json
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return doc.RootElement.GetString() ?? content;
            }
        }
        catch
        {
            // Fallback
        }

        if (content.StartsWith("\"") && content.EndsWith("\""))
        {
            return content.Trim('"');
        }

        return content;
    }

    public async Task<LoginResponse?> LoginPasskeyAsync(string credentialJson)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("account/assertion/result", new { CredentialJson = credentialJson });
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
