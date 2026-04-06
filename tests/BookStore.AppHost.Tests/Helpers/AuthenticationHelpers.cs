using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ServiceDefaults;
using BookStore.Shared;
using JasperFx;
using Refit;

namespace BookStore.AppHost.Tests.Helpers;

public static class AuthenticationHelpers
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string GetServiceBaseUrl(HttpClient client)
        => client.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("The provided HttpClient does not have a BaseAddress.");

    public static async Task<LoginResponse?> LoginAsAdminAsync(string tenantId)
    {
        var app = GlobalHooks.App!;
        using var keycloakClient = app.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = GetServiceBaseUrl(keycloakClient);

        var tenantAlias = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? MultiTenancyConstants.DefaultTenantAlias
            : tenantId;

        return await LoginAsUserAsync(keycloakClient, keycloakUrl, $"admin@{tenantAlias}.com", "Admin123!");
    }

    public static Task<LoginResponse?> LoginAsAdminAsync(HttpClient client, string keycloakUrl)
        => LoginAsUserAsync(client, keycloakUrl, "admin@default.com", "Admin123!");

    public static async Task<LoginResponse?> LoginAsUserAsync(
        HttpClient client,
        string keycloakUrl,
        string username,
        string password)
    {
        using var response = await RequestPasswordGrantAsync(client, keycloakUrl, username, password);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
    }

    public static Task<HttpResponseMessage> RequestPasswordGrantAsync(
        HttpClient client,
        string keycloakUrl,
        string username,
        string password)
        => RequestPasswordGrantAsync(
            client,
            keycloakUrl,
            realm: "bookstore",
            clientId: "bookstore-web",
            username,
            password);

    public static Task<HttpResponseMessage> RequestPasswordGrantAsync(
        HttpClient client,
        string keycloakUrl,
        string realm,
        string clientId,
        string username,
        string password)
    {
        var tokenUri = BuildUri(keycloakUrl, $"/realms/{realm}/protocol/openid-connect/token");

        var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            ])
        };

        return client.SendAsync(request);
    }

    public static async Task<string> GetKeycloakAdminTokenAsync(HttpClient client, string keycloakUrl)
    {
        if (string.IsNullOrWhiteSpace(GlobalHooks.KeycloakAdminUsername)
            || string.IsNullOrWhiteSpace(GlobalHooks.KeycloakAdminPassword))
        {
            throw new InvalidOperationException("Keycloak admin credentials were not initialized by GlobalSetup.");
        }

        using var response = await RequestPasswordGrantAsync(
            client,
            keycloakUrl,
            realm: "master",
            clientId: "admin-cli",
            username: GlobalHooks.KeycloakAdminUsername,
            password: GlobalHooks.KeycloakAdminPassword);

        _ = response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Keycloak admin token response did not include an access token.");
        }

        return tokenResponse.AccessToken;
    }

    public static async Task<string> CreateTestUserInKeycloakAsync(
        HttpClient client,
        string keycloakAdminToken,
        string email,
        string password,
        string tenantId,
        string role)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, BuildRelativeUri("/admin/realms/bookstore/users"))
        {
            Content = JsonContent.Create(new
            {
                username = email,
                email,
                enabled = true,
                emailVerified = true,
                attributes = new Dictionary<string, string[]>
                {
                    ["tenant_id"] = [tenantId]
                },
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = password,
                        temporary = false
                    }
                }
            })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keycloakAdminToken);

        using var createResponse = await client.SendAsync(createRequest);
        if (createResponse.StatusCode != HttpStatusCode.Created)
        {
            var content = await createResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Keycloak rejected test user creation for {email} with status {(int)createResponse.StatusCode}: {content}");
        }

        var userId = createResponse.Headers.Location?.Segments.LastOrDefault()?.Trim('/');
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = await ResolveKeycloakUserIdByEmailAsync(client, keycloakAdminToken, email);
        }

        using var roleRequest = new HttpRequestMessage(HttpMethod.Get, BuildRelativeUri($"/admin/realms/bookstore/roles/{Uri.EscapeDataString(role)}"));
        roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keycloakAdminToken);

        using var roleResponse = await client.SendAsync(roleRequest);
        _ = roleResponse.EnsureSuccessStatusCode();

        var roleRepresentation = await roleResponse.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(JsonOptions)
            ?? throw new InvalidOperationException($"Keycloak did not return role metadata for role '{role}'.");

        using var mappingRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildRelativeUri($"/admin/realms/bookstore/users/{Uri.EscapeDataString(userId)}/role-mappings/realm"))
        {
            Content = JsonContent.Create(new[] { roleRepresentation })
        };
        mappingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keycloakAdminToken);

        using var mappingResponse = await client.SendAsync(mappingRequest);
        _ = mappingResponse.EnsureSuccessStatusCode();

        return userId;
    }

    public static async Task<UserClient> CreateUserAndGetClientAsync(string? tenantId = null)
    {
        var app = GlobalHooks.App!;
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;

        using var keycloakClient = app.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = GetServiceBaseUrl(keycloakClient);
        var keycloakAdminToken = await GetKeycloakAdminTokenAsync(keycloakClient, keycloakUrl);

        var email = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        _ = await CreateTestUserInKeycloakAsync(
            keycloakClient,
            keycloakAdminToken,
            email,
            password,
            actualTenantId,
            "User");

        var tokenResponse = await LoginAsUserAsync(keycloakClient, keycloakUrl, email, password)
            ?? throw new InvalidOperationException($"Failed to login test user '{email}' through Keycloak.");

        var userId = GetUserIdFromToken(tokenResponse.AccessToken);

        var authenticatedClient = app.CreateHttpClient(ResourceNames.ApiService);
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

        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = GetServiceBaseUrl(keycloakClient);
        var keycloakAdminToken = await GetKeycloakAdminTokenAsync(keycloakClient, keycloakUrl);

        _ = await CreateTestUserInKeycloakAsync(
            keycloakClient,
            keycloakAdminToken,
            email,
            password,
            tenantId,
            "User");

        var tokenResponse = await LoginAsUserAsync(keycloakClient, keycloakUrl, email, password);
        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Login response was null.");
        }

        return (email, password, tokenResponse, tenantId);
    }

    public static Guid GetUserIdFromToken(string accessToken)
    {
        var sub = GetStringClaimFromToken(accessToken, "sub");
        return Guid.TryParse(sub, out var userId)
            ? userId
            : throw new InvalidOperationException("The token does not contain a GUID 'sub' claim.");
    }

    public static string? GetStringClaimFromToken(string accessToken, string claimName)
    {
        var payload = GetTokenPayload(accessToken);
        if (!payload.TryGetProperty(claimName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.GetRawText();
    }

    public static string[] GetStringArrayClaimFromToken(string accessToken, string claimName)
    {
        var payload = GetTokenPayload(accessToken);
        if (!payload.TryGetProperty(claimName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. element.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()];
    }

    public static string[] GetNestedStringArrayClaimFromToken(string accessToken, string parentClaim, string childClaim)
    {
        var payload = GetTokenPayload(accessToken);
        if (!payload.TryGetProperty(parentClaim, out var parentElement)
            || parentElement.ValueKind != JsonValueKind.Object
            || !parentElement.TryGetProperty(childClaim, out var childElement)
            || childElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. childElement.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()];
    }

    static async Task<string> ResolveKeycloakUserIdByEmailAsync(HttpClient client, string keycloakAdminToken, string email)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildRelativeUri($"/admin/realms/bookstore/users?email={Uri.EscapeDataString(email)}&exact=true"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keycloakAdminToken);

        using var response = await client.SendAsync(request);
        _ = response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<KeycloakUserRepresentation[]>(JsonOptions) ?? [];
        var userId = users.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException($"Failed to resolve Keycloak user id for '{email}'.");
        }

        return userId;
    }

    static Uri BuildUri(string baseUrl, string relativePath)
    {
        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/";

        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), relativePath.TrimStart('/'));
    }

    static Uri BuildRelativeUri(string relativePath) => new(relativePath, UriKind.Relative);

    static JsonElement GetTokenPayload(string accessToken)
    {
        var segments = accessToken.Split('.');
        if (segments.Length < 2)
        {
            throw new InvalidOperationException("The provided token is not a valid JWT.");
        }

        var payloadSegment = segments[1]
            .Replace('-', '+')
            .Replace('_', '/');

        payloadSegment = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');

        var payloadBytes = Convert.FromBase64String(payloadSegment);
        using var document = JsonDocument.Parse(payloadBytes);
        return document.RootElement.Clone();
    }

    public sealed record LoginResponse(
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);

    sealed record KeycloakRoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("composite")] bool Composite,
        [property: JsonPropertyName("clientRole")] bool ClientRole,
        [property: JsonPropertyName("containerId")] string ContainerId);

    sealed record KeycloakUserRepresentation([property: JsonPropertyName("id")] string Id);

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
