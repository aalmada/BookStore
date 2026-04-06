using System.Net.Http.Headers;
using System.Text.Json;
using BookStore.Web.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace BookStore.Web.Services;

/// <summary>
/// Stores Keycloak access tokens in a cache shared across request and circuit scopes.
/// </summary>
public class KeycloakTokenAccessor(
    IMemoryCache memoryCache,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<KeycloakTokenAccessor> logger)
{
    static readonly TimeSpan RefreshBuffer = TimeSpan.FromSeconds(30);
    const string AccessTokenCacheKeyPrefix = "kc_token:access:";
    const string RefreshTokenCacheKeyPrefix = "kc_token:refresh:";
    const string KeycloakTokenClientName = "keycloak-token";
    const string KeycloakTokenEndpointSuffix = "/realms/bookstore/protocol/openid-connect/token";

    public void SetToken(string userSub, string accessToken, DateTimeOffset expiry)
    {
        if (string.IsNullOrWhiteSpace(userSub) || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        _ = memoryCache.Set(GetAccessCacheKey(userSub), accessToken, expiry);
    }

    public void SetRefreshToken(string userSub, string refreshToken, DateTimeOffset expiry)
    {
        if (string.IsNullOrWhiteSpace(userSub) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        _ = memoryCache.Set(GetRefreshCacheKey(userSub), refreshToken, expiry);
    }

    public void SetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (!TryReadJwtSub(token, out var userSub) || !TryReadJwtExpiry(token, out var expiry))
        {
            return;
        }

        SetToken(userSub, token, expiry);
    }

    public async Task<string?> GetAccessTokenAsync(string userSub)
    {
        if (string.IsNullOrWhiteSpace(userSub))
        {
            return null;
        }

        _ = memoryCache.TryGetValue(GetAccessCacheKey(userSub), out string? accessToken);
        if (!string.IsNullOrWhiteSpace(accessToken)
            && TryReadJwtExpiry(accessToken, out var accessExpiry)
            && accessExpiry > DateTimeOffset.UtcNow.Add(RefreshBuffer))
        {
            return accessToken;
        }

        _ = memoryCache.TryGetValue(GetRefreshCacheKey(userSub), out string? refreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        return await TryRefreshAccessTokenAsync(userSub, refreshToken);
    }

    async Task<string?> TryRefreshAccessTokenAsync(string userSub, string refreshToken)
    {
        var keycloakBaseUrl = configuration["ConnectionStrings:keycloak"]
            ?? throw new InvalidOperationException("Keycloak connection string not configured");

        var tokenEndpoint = $"{keycloakBaseUrl.TrimEnd('/')}{KeycloakTokenEndpointSuffix}";
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", "bookstore-web"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            ])
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var client = httpClientFactory.CreateClient(KeycloakTokenClientName);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                RemoveTokens(userSub);
                Log.TokenRefreshFailed(logger, userSub);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (!TryGetStringProperty(document.RootElement, "access_token", out var newAccessToken)
                || string.IsNullOrWhiteSpace(newAccessToken))
            {
                RemoveTokens(userSub);
                Log.TokenRefreshFailed(logger, userSub);
                return null;
            }

            var newRefreshToken = refreshToken;
            _ = TryGetStringProperty(document.RootElement, "refresh_token", out newRefreshToken);
            newRefreshToken = string.IsNullOrWhiteSpace(newRefreshToken) ? refreshToken : newRefreshToken;

            var accessExpiry = ParseJwtExpiryOrFallback(newAccessToken, DateTimeOffset.UtcNow.AddMinutes(5));
            var refreshExpiry = ParseJwtExpiryOrFallback(newRefreshToken, DateTimeOffset.UtcNow.AddMinutes(30));

            SetToken(userSub, newAccessToken, accessExpiry);
            SetRefreshToken(userSub, newRefreshToken, refreshExpiry);

            Log.AccessTokenRefreshed(logger, userSub);
            return newAccessToken;
        }
        catch (HttpRequestException)
        {
            RemoveTokens(userSub);
            Log.TokenRefreshFailed(logger, userSub);
            return null;
        }
        catch (TaskCanceledException)
        {
            RemoveTokens(userSub);
            Log.TokenRefreshFailed(logger, userSub);
            return null;
        }
        catch (JsonException)
        {
            RemoveTokens(userSub);
            Log.TokenRefreshFailed(logger, userSub);
            return null;
        }
    }

    void RemoveTokens(string userSub)
    {
        memoryCache.Remove(GetAccessCacheKey(userSub));
        memoryCache.Remove(GetRefreshCacheKey(userSub));
    }

    static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var propertyValue = property.GetString();
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            return false;
        }

        value = propertyValue;
        return true;
    }

    static DateTimeOffset ParseJwtExpiryOrFallback(string token, DateTimeOffset fallback)
    {
        if (!TryReadJwtExpiry(token, out var expiry))
        {
            return fallback;
        }

        return expiry;
    }

    static string GetAccessCacheKey(string userSub) => $"{AccessTokenCacheKeyPrefix}{userSub}";
    static string GetRefreshCacheKey(string userSub) => $"{RefreshTokenCacheKeyPrefix}{userSub}";

    static bool TryReadJwtSub(string token, out string userSub)
    {
        userSub = string.Empty;

        if (!TryParseJwtPayload(token, out var payload))
        {
            return false;
        }

        if (!payload.TryGetProperty("sub", out var subProperty) || subProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = subProperty.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        userSub = value;
        return true;
    }

    static bool TryReadJwtExpiry(string token, out DateTimeOffset expiresAtUtc)
    {
        expiresAtUtc = default;

        if (!TryParseJwtPayload(token, out var payload))
        {
            return false;
        }

        if (!payload.TryGetProperty("exp", out var expProperty))
        {
            return false;
        }

        if (!TryGetUnixSeconds(expProperty, out var unixSeconds))
        {
            return false;
        }

        try
        {
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);
        }

        return true;
    }

    static bool TryParseJwtPayload(string token, out JsonElement payload)
    {
        payload = default;

        var segments = token.Split('.');
        if (segments.Length < 2)
        {
            return false;
        }

        try
        {
            var payloadBytes = DecodeBase64Url(segments[1]);
            using var document = JsonDocument.Parse(payloadBytes);
            payload = document.RootElement.Clone();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static bool TryGetUnixSeconds(JsonElement expProperty, out long unixSeconds)
    {
        if (expProperty.ValueKind == JsonValueKind.Number)
        {
            return expProperty.TryGetInt64(out unixSeconds);
        }

        if (expProperty.ValueKind == JsonValueKind.String)
        {
            return long.TryParse(expProperty.GetString(), out unixSeconds);
        }

        unixSeconds = default;
        return false;
    }

    static byte[] DecodeBase64Url(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');

        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        return Convert.FromBase64String(normalized);
    }
}
