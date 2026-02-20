using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.Shared;
using JasperFx;
using Refit;

namespace BookStore.AppHost.Tests.Helpers;

public static class AuthenticationHelpers
{
    public static async Task<LoginResponse?> LoginAsAdminAsync(string tenantId)
    {
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        return await LoginAsAdminAsync(client, tenantId);
    }

    public static async Task<LoginResponse?> LoginAsAdminAsync(HttpClient client, string tenantId)
    {
        var tenantAlias = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? MultiTenancyConstants.DefaultTenantAlias
            : tenantId;
        var email = $"admin@{tenantAlias}.com";

        var credentials = new { email, password = "Admin123!" };

        // Simple retry logic
        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/account/login")
            {
                Content = JsonContent.Create(credentials)
            };
            request.Headers.Add("X-Tenant-ID", tenantId);

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginResponse>();
            }

            if (i == 2) // Last attempt
            {
                return null;
            }

            await Task.Delay(TestConstants.DefaultPollingInterval); // Wait before retry
        }

        return null;
    }

    public static async Task<UserClient> CreateUserAndGetClientAsync(string? tenantId = null)
    {
        var app = GlobalHooks.App!;
        var publicClient = app.CreateHttpClient("apiservice");
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        publicClient.DefaultRequestHeaders.Add("X-Tenant-ID", actualTenantId);

        var email = $"user_{Guid.CreateVersion7()}@example.com";
        var password = FakeDataGenerators.GenerateFakePassword();

        // Register
        var registerRequest = new { email, password };
        var registerResponse = await publicClient.PostAsJsonAsync("/account/register", registerRequest);
        if (!registerResponse.IsSuccessStatusCode)
        {
        }

        _ = registerResponse.EnsureSuccessStatusCode();

        // Login
        var loginRequest = new { email, password };
        var loginResponse = await publicClient.PostAsJsonAsync("/account/login", loginRequest);
        if (!loginResponse.IsSuccessStatusCode)
        {
        }

        _ = loginResponse.EnsureSuccessStatusCode();

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Decode JWT to verify claims
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        _ = handler.ReadJwtToken(tokenResponse!.AccessToken);

        var userId = Guid.Parse(handler.ReadJwtToken(tokenResponse!.AccessToken).Claims.First(c => c.Type == "sub")
            .Value);

        // Create authenticated client
        var authenticatedClient = app.CreateHttpClient("apiservice");
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
        authenticatedClient.DefaultRequestHeaders.Add("X-Tenant-ID", actualTenantId);

        return new UserClient(authenticatedClient, userId);
    }

    public record UserClient(HttpClient Client, Guid UserId);

    public static async Task<T> CreateUserAndGetClientAsync<T>(string? tenantId = null)
    {
        var userClient = await CreateUserAndGetClientAsync(tenantId);
        return RestService.For<T>(userClient.Client);
    }

    public static async Task<(string email, string password, LoginResponse loginResponse, string tenantId)>
        RegisterAndLoginUserAsync(string? tenantId = null, string? email = null)
    {
        tenantId ??= StorageConstants.DefaultTenantId;
        email ??= FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var client = HttpClientHelpers.GetUnauthenticatedClient(tenantId);
        var registerResponse = await client.PostAsJsonAsync("/account/register", new { email, password });
        _ = registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/account/login", new { email, password });
        _ = loginResponse.EnsureSuccessStatusCode();

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Login response was null.");
        }

        return (email, password, tokenResponse, tenantId);
    }

    public record LoginResponse(
        string TokenType,
        string AccessToken,
        int ExpiresIn,
        string RefreshToken,
        Guid? UserId = null);

    public record ErrorResponse(
        [property: JsonPropertyName("error")]
        string Error,
        string Message);

    public record MessageResponse(string Message);

    public record ValidationProblemDetails(
        string? Title = null,
        int? Status = null,
        string? Detail = null,
        [property: JsonPropertyName("error")]
        string? Error = null);
}
