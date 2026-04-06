using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.Shared.Models;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.Auth;

public sealed partial class KeycloakAdminService(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakAdminService> logger)
    : IKeycloakAdminService
{
    readonly HttpClient _httpClient = httpClient;
    readonly KeycloakAdminOptions _options = options.Value;
    readonly ILogger<KeycloakAdminService> _logger = logger;

    public async Task<Result<string>> CreateUserAsync(
        string tenantId,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<string>(Error.Validation(ErrorCodes.Tenancy.TenantIdRequired, "Tenant ID is required."));
        }

        if (!BookStore.Shared.Validation.EmailValidator.IsValid(email))
        {
            return Result.Failure<string>(Error.Validation(ErrorCodes.Tenancy.InvalidAdminEmail, "Invalid admin email."));
        }

        var (isPasswordValid, passwordErrors) = BookStore.Shared.Validation.PasswordValidator.Validate(password);
        if (!isPasswordValid)
        {
            return Result.Failure<string>(Error.Validation(
                ErrorCodes.Tenancy.InvalidAdminPassword,
                passwordErrors.FirstOrDefault() ?? "Invalid admin password."));
        }

        var adminTokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (adminTokenResult.IsFailure)
        {
            return Result.Failure<string>(adminTokenResult.Error);
        }

        Log.CreatingUser(_logger, tenantId, email);

        var createResult = await CreateUserInternalAsync(
            adminTokenResult.Value,
            tenantId,
            email,
            password,
            cancellationToken);

        if (createResult.IsFailure)
        {
            return Result.Failure<string>(createResult.Error);
        }

        var roleResult = await AssignAdminRoleAsync(adminTokenResult.Value, createResult.Value, cancellationToken);
        if (roleResult.IsFailure)
        {
            var deleteUserResult = await DeleteUserAsync(createResult.Value, cancellationToken);
            if (deleteUserResult.IsFailure)
            {
                Log.RoleAssignmentCompensationFailed(
                    _logger,
                    createResult.Value,
                    roleResult.Error.Code,
                    deleteUserResult.Error.Code);
            }

            return Result.Failure<string>(roleResult.Error);
        }

        Log.UserCreated(_logger, email, tenantId, createResult.Value);
        return Result.Success(createResult.Value);
    }

    public async Task<Result> DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keycloakUserId))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.Auth.InvalidRequest,
                "Keycloak user ID is required for deletion."));
        }

        var adminTokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (adminTokenResult.IsFailure)
        {
            return Result.Failure(adminTokenResult.Error);
        }

        var deleteUri = BuildUri($"/admin/realms/{Uri.EscapeDataString(_options.Realm)}/users/{Uri.EscapeDataString(keycloakUserId)}");
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteUri);
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokenResult.Value);

        HttpResponseMessage deleteResponse;
        try
        {
            deleteResponse = await _httpClient.SendAsync(deleteRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.DeleteUserRequestFailed(_logger, keycloakUserId, ex);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to call Keycloak user deletion API."));
        }

        if (deleteResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Log.DeleteUserNotFound(_logger, keycloakUserId);
            return Result.Success();
        }

        if (!deleteResponse.IsSuccessStatusCode)
        {
            var body = await deleteResponse.Content.ReadAsStringAsync(cancellationToken);
            Log.DeleteUserRejected(_logger, keycloakUserId, (int)deleteResponse.StatusCode, body);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Keycloak rejected user deletion request."));
        }

        Log.UserDeleted(_logger, keycloakUserId);
        return Result.Success();
    }

    async Task<Result<string>> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.AdminUsername)
            || string.IsNullOrWhiteSpace(_options.AdminPassword)
            || string.IsNullOrWhiteSpace(_options.Realm))
        {
            Log.AdminConfigMissing(_logger);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.InvalidRequest,
                "Keycloak admin configuration is missing. Configure Keycloak:Admin settings."));
        }

        var tokenEndpoint = BuildUri("/realms/master/protocol/openid-connect/token");
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", "admin-cli"),
                new KeyValuePair<string, string>("username", _options.AdminUsername),
                new KeyValuePair<string, string>("password", _options.AdminPassword)
            ])
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(tokenRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.TokenRequestFailed(_logger, ex);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to connect to Keycloak admin token endpoint."));
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.TokenRequestRejected(_logger, (int)response.StatusCode, body);
            return Result.Failure<string>(Error.Unauthorized(
                ErrorCodes.Auth.InvalidCredentials,
                "Failed to authenticate with Keycloak admin API."));
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var accessToken = document.RootElement.GetProperty("access_token").GetString();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return Result.Failure<string>(Error.Failure(
                    ErrorCodes.Auth.RequestFailed,
                    "Keycloak admin API did not return an access token."));
            }

            return Result.Success(accessToken);
        }
        catch (Exception ex)
        {
            Log.TokenParseFailed(_logger, ex);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to parse Keycloak admin token response."));
        }
    }

    async Task<Result<string>> CreateUserInternalAsync(
        string adminAccessToken,
        string tenantId,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var usersUri = BuildUri($"/admin/realms/{Uri.EscapeDataString(_options.Realm)}/users");

        var payload = new
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
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, usersUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.CreateUserRequestFailed(_logger, email, ex);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to call Keycloak user creation API."));
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return Result.Failure<string>(Error.Conflict(
                ErrorCodes.Auth.DuplicateEmail,
                $"A Keycloak user with email '{email}' already exists."));
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.CreateUserRejected(_logger, email, (int)response.StatusCode, body);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Keycloak rejected user creation request."));
        }

        var userId = TryExtractUserIdFromLocation(response.Headers.Location);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return Result.Success(userId);
        }

        return await ResolveUserIdByEmailAsync(adminAccessToken, email, cancellationToken);
    }

    async Task<Result> AssignAdminRoleAsync(string adminAccessToken, string userId, CancellationToken cancellationToken)
    {
        var roleUri = BuildUri($"/admin/realms/{Uri.EscapeDataString(_options.Realm)}/roles/Admin");

        using var roleRequest = new HttpRequestMessage(HttpMethod.Get, roleUri);
        roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);

        HttpResponseMessage roleResponse;
        try
        {
            roleResponse = await _httpClient.SendAsync(roleRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.RoleLookupFailed(_logger, ex);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to retrieve Keycloak Admin role."));
        }

        if (!roleResponse.IsSuccessStatusCode)
        {
            var body = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
            Log.RoleLookupRejected(_logger, (int)roleResponse.StatusCode, body);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Keycloak Admin role lookup failed."));
        }

        KeycloakRoleRepresentation? role;
        try
        {
            role = await roleResponse.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.RoleParseFailed(_logger, ex);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to parse Keycloak role payload."));
        }

        if (role == null)
        {
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Keycloak Admin role payload was empty."));
        }

        var mappingUri = BuildUri($"/admin/realms/{Uri.EscapeDataString(_options.Realm)}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm");

        using var mappingRequest = new HttpRequestMessage(HttpMethod.Post, mappingUri)
        {
            Content = JsonContent.Create(new[] { role })
        };
        mappingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);

        HttpResponseMessage mappingResponse;
        try
        {
            mappingResponse = await _httpClient.SendAsync(mappingRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.RoleAssignmentFailed(_logger, ex);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to assign Admin role in Keycloak."));
        }

        if (!mappingResponse.IsSuccessStatusCode)
        {
            var body = await mappingResponse.Content.ReadAsStringAsync(cancellationToken);
            Log.RoleAssignmentRejected(_logger, (int)mappingResponse.StatusCode, body);
            return Result.Failure(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Keycloak rejected Admin role assignment."));
        }

        return Result.Success();
    }

    async Task<Result<string>> ResolveUserIdByEmailAsync(
        string adminAccessToken,
        string email,
        CancellationToken cancellationToken)
    {
        var query = $"/admin/realms/{Uri.EscapeDataString(_options.Realm)}/users?email={Uri.EscapeDataString(email)}&exact=true";
        var uri = BuildUri(query);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.UserLookupFailed(_logger, email, ex);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to query created user from Keycloak."));
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.UserLookupRejected(_logger, email, (int)response.StatusCode, body);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Keycloak user lookup failed after creation."));
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("id", out var idElement))
                    {
                        var id = idElement.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            return Result.Success(id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.UserLookupParseFailed(_logger, email, ex);
            return Result.Failure<string>(Error.Failure(
                ErrorCodes.Auth.RequestFailed,
                "Failed to parse Keycloak user lookup response."));
        }

        return Result.Failure<string>(Error.Failure(
            ErrorCodes.Auth.RequestFailed,
            "Created Keycloak user could not be resolved."));
    }

    Uri BuildUri(string path)
    {
        var baseUri = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : $"{_options.BaseUrl}/";
        return new Uri(new Uri(baseUri, UriKind.Absolute), path.TrimStart('/'));
    }

    static string? TryExtractUserIdFromLocation(Uri? location)
    {
        if (location == null)
        {
            return null;
        }

        var lastSegment = location.Segments.LastOrDefault();
        return string.IsNullOrWhiteSpace(lastSegment)
            ? null
            : lastSegment.Trim('/');
    }

    sealed record KeycloakRoleRepresentation(string Id, string Name, bool Composite, bool ClientRole, string ContainerId);

    static partial class Log
    {
        [LoggerMessage(EventId = 9200, Level = LogLevel.Information, Message = "Creating Keycloak user {Email} for tenant {TenantId}")]
        public static partial void CreatingUser(ILogger logger, string tenantId, string email);

        [LoggerMessage(EventId = 9201, Level = LogLevel.Information, Message = "Created Keycloak user {Email} for tenant {TenantId} with user id {UserId}")]
        public static partial void UserCreated(ILogger logger, string email, string tenantId, string userId);

        [LoggerMessage(EventId = 9202, Level = LogLevel.Error, Message = "Keycloak admin configuration is missing required values")]
        public static partial void AdminConfigMissing(ILogger logger);

        [LoggerMessage(EventId = 9203, Level = LogLevel.Error, Message = "Keycloak admin token request failed")]
        public static partial void TokenRequestFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9204, Level = LogLevel.Warning, Message = "Keycloak admin token request rejected with status {StatusCode}: {ResponseBody}")]
        public static partial void TokenRequestRejected(ILogger logger, int statusCode, string responseBody);

        [LoggerMessage(EventId = 9205, Level = LogLevel.Error, Message = "Failed to parse Keycloak admin token payload")]
        public static partial void TokenParseFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9206, Level = LogLevel.Error, Message = "Keycloak create-user request failed for {Email}")]
        public static partial void CreateUserRequestFailed(ILogger logger, string email, Exception exception);

        [LoggerMessage(EventId = 9207, Level = LogLevel.Warning, Message = "Keycloak create-user request rejected for {Email} with status {StatusCode}: {ResponseBody}")]
        public static partial void CreateUserRejected(ILogger logger, string email, int statusCode, string responseBody);

        [LoggerMessage(EventId = 9208, Level = LogLevel.Error, Message = "Keycloak Admin role lookup failed")]
        public static partial void RoleLookupFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9209, Level = LogLevel.Warning, Message = "Keycloak Admin role lookup rejected with status {StatusCode}: {ResponseBody}")]
        public static partial void RoleLookupRejected(ILogger logger, int statusCode, string responseBody);

        [LoggerMessage(EventId = 9210, Level = LogLevel.Error, Message = "Failed to parse Keycloak role response")]
        public static partial void RoleParseFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9211, Level = LogLevel.Error, Message = "Keycloak role assignment call failed")]
        public static partial void RoleAssignmentFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9212, Level = LogLevel.Warning, Message = "Keycloak role assignment rejected with status {StatusCode}: {ResponseBody}")]
        public static partial void RoleAssignmentRejected(ILogger logger, int statusCode, string responseBody);

        [LoggerMessage(EventId = 9213, Level = LogLevel.Error, Message = "Keycloak user lookup failed for {Email}")]
        public static partial void UserLookupFailed(ILogger logger, string email, Exception exception);

        [LoggerMessage(EventId = 9214, Level = LogLevel.Warning, Message = "Keycloak user lookup rejected for {Email} with status {StatusCode}: {ResponseBody}")]
        public static partial void UserLookupRejected(ILogger logger, string email, int statusCode, string responseBody);

        [LoggerMessage(EventId = 9215, Level = LogLevel.Error, Message = "Failed to parse Keycloak user lookup response for {Email}")]
        public static partial void UserLookupParseFailed(ILogger logger, string email, Exception exception);

        [LoggerMessage(EventId = 9217, Level = LogLevel.Error, Message = "Keycloak delete-user request failed for user id {UserId}")]
        public static partial void DeleteUserRequestFailed(ILogger logger, string userId, Exception exception);

        [LoggerMessage(EventId = 9218, Level = LogLevel.Warning, Message = "Keycloak delete-user request rejected for user id {UserId} with status {StatusCode}: {ResponseBody}")]
        public static partial void DeleteUserRejected(ILogger logger, string userId, int statusCode, string responseBody);

        [LoggerMessage(EventId = 9219, Level = LogLevel.Information, Message = "Keycloak user {UserId} not found during delete; assuming already removed")]
        public static partial void DeleteUserNotFound(ILogger logger, string userId);

        [LoggerMessage(EventId = 9220, Level = LogLevel.Information, Message = "Deleted Keycloak user {UserId}")]
        public static partial void UserDeleted(ILogger logger, string userId);

        [LoggerMessage(EventId = 9221, Level = LogLevel.Error, Message = "Keycloak role assignment failed for user {UserId} with error code {RoleErrorCode}, and compensation delete also failed with error code {DeleteErrorCode}")]
        public static partial void RoleAssignmentCompensationFailed(ILogger logger, string userId, string roleErrorCode, string deleteErrorCode);
    }
}
