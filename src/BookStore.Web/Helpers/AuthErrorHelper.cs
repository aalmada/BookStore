using Microsoft.Extensions.Localization;

namespace BookStore.Web.Helpers;

public class AuthErrorHelper
{
    readonly IStringLocalizer<AuthErrorHelper> _localizer;

    public AuthErrorHelper(IStringLocalizer<AuthErrorHelper> localizer) => _localizer = localizer;

    public string GetFriendlyErrorMessage(string? backendError)
    {
        if (string.IsNullOrWhiteSpace(backendError))
        {
            return _localizer["DefaultError"];
        }

        // Clean up JS stack trace if present (common in Blazor JS interop exceptions)
        var message = backendError;
        if (message.Contains(" at ") && (message.Contains(".js:") || message.Contains(" (")))
        {
            message = message.Split('\n')[0].Split(" at ")[0].Trim();
        }

        var errorLower = message.ToLowerInvariant();

        if (errorLower.Contains("fetch") ||
            errorLower.Contains("network") ||
            errorLower.Contains("connection") ||
            errorLower.Contains("failed to fetch"))
        {
            return _localizer["ConnectionError"];
        }

        if (errorLower.Contains("401") ||
            errorLower.Contains("unauthorized") ||
            errorLower.Contains("invalid username or password") ||
            errorLower.Contains("invalid email or password"))
        {
            return _localizer["InvalidCredentials"];
        }

        if (errorLower.Contains("locked out"))
        {
            return _localizer["AccountLocked"];
        }

        if (errorLower.Contains("requires verification") ||
            errorLower.Contains("email not confirmed"))
        {
            return _localizer["VerificationRequired"];
        }

        if (errorLower.Contains("passkey login failed") ||
            errorLower.Contains("invalid passkey assertion"))
        {
            return _localizer["PasskeyLoginFailed"];
        }

        if (errorLower.Contains("no passkeys registered") ||
            errorLower.Contains("user not found"))
        {
            return _localizer["NoPasskeysRegistered"];
        }

        if (errorLower.Contains("already registered"))
        {
            return "This authenticator is already registered for this account.";
        }

        if (errorLower.Contains("400") ||
            errorLower.Contains("bad request") ||
            errorLower.Contains("status code"))
        {
            return _localizer["InvalidRequest"];
        }

        // Return original error if it seems safe and we don't have a specific mapping, 
        // OR fallback to default. 
        // For security, we might want to mask unknown errors, 
        // but often backend returns validation errors we want to show (e.g. "Password too short").
        // Let's return the original if it's not one of the technical ones above, 
        // assuming Refit/API returns safe messages for business logic errors.
        return message;
    }
}
