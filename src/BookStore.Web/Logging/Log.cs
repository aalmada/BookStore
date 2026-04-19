using Microsoft.Extensions.Logging;

namespace BookStore.Web.Logging;

public static partial class Log
{
    // ReactiveQuery
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "ReactiveQuery failed to load data.")]
    public static partial void QueryLoadFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "ReactiveQuery load cancelled for keys: {Keys}")]
    public static partial void QueryLoadCancelled(ILogger logger, string keys);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "ReactiveQuery invalidating due to event: {EventType}. Matched Keys: {Keys}")]
    public static partial void QueryInvalidating(ILogger logger, string eventType, string keys);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to mutate data optimistically.")]
    public static partial void MutationFailed(ILogger logger, Exception ex);

    // PasskeyService
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to get creation options: {StatusCode} {Error}")]
    public static partial void RegistrationOptionsFailed(ILogger logger, System.Net.HttpStatusCode statusCode,
        string error);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error getting passkey creation options")]
    public static partial void RegistrationOptionsError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to get login options: {StatusCode}")]
    public static partial void LoginOptionsFailed(ILogger logger, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error getting passkey login options")]
    public static partial void LoginOptionsError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Passkey registration failed: {StatusCode} - {Error}")]
    public static partial void RegistrationResultFailed(ILogger logger, System.Net.HttpStatusCode statusCode,
        string error);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error registering passkey")]
    public static partial void RegistrationCompleteError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Passkey login failed: {StatusCode} - {Error}")]
    public static partial void
        AssertionResultFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string error);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error during passkey login")]
    public static partial void LoginCompleteError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error logging in with passkey")]
    public static partial void PasskeyLoginError(ILogger logger, Exception ex);

    // CatalogService
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to toggle favorite for book {BookId}")]
    public static partial void FavoriteToggleFailed(ILogger logger, Guid bookId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to rate book {BookId}")]
    public static partial void RatingFailed(ILogger logger, Guid bookId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to remove rating for book {BookId}")]
    public static partial void RatingRemovalFailed(ILogger logger, Guid bookId, Exception ex);

    // JwtAuthenticationStateProvider
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to hydrate authentication token from local storage for tenant {TenantId}.")]
    public static partial void AuthStateStorageHydrationFailed(ILogger logger, string tenantId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to parse or validate authentication token for tenant {TenantId}.")]
    public static partial void AuthStateTokenReadFailed(ILogger logger, string tenantId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to refresh access token for tenant {TenantId}.")]
    public static partial void AuthStateRefreshFailed(ILogger logger, string tenantId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to persist refreshed authentication tokens to local storage for tenant {TenantId}.")]
    public static partial void AuthStateStorageWriteFailed(ILogger logger, string tenantId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to persist authentication token to local storage for tenant {TenantId}.")]
    public static partial void AuthStateAuthenticationPersistFailed(ILogger logger, string tenantId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to remove authentication tokens from local storage for tenant {TenantId}.")]
    public static partial void AuthStateStorageDeleteFailed(ILogger logger, string tenantId, Exception ex);
}
