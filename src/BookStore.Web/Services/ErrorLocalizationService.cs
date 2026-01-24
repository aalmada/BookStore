using BookStore.Shared.Models;
using Microsoft.Extensions.Localization;

namespace BookStore.Web.Services;

public class ErrorLocalizationService
{
    readonly IStringLocalizer<ErrorLocalizationService> _localizer;

    public ErrorLocalizationService(IStringLocalizer<ErrorLocalizationService> localizer) => _localizer = localizer;

    public string GetLocalizedMessage(Error? error)
    {
        if (error == null || string.IsNullOrWhiteSpace(error.Code) || error.Code == "None")
        {
            return _localizer["DefaultError"];
        }

        // Try to get a specific translation for the error code
        var localized = _localizer[error.Code];
        if (!localized.ResourceNotFound)
        {
            return localized;
        }

        // If the code itself is not found, check if we have a default for the error type
        var typeDefault = _localizer[$"ErrorType_{error.Type}"];
        if (!typeDefault.ResourceNotFound)
        {
            return typeDefault;
        }

        // If we have a message from the backend, use it as a fallback if it's likely user-friendly
        // otherwise return a generic error.
        if (!string.IsNullOrWhiteSpace(error.Message) && !IsTechnicalMessage(error.Message))
        {
            return error.Message;
        }

        return _localizer["DefaultError"];
    }

    public string GetLocalizedMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return _localizer["DefaultError"];
        }

        // Clean up JS stack trace if present
        if (message.Contains(" at ") && (message.Contains(".js:") || message.Contains(" (")))
        {
            message = message.Split('\n')[0].Split(" at ")[0].Trim();
        }

        // Check if the message matches some known patterns (legacy support for string errors)
        var errorLower = message.ToLowerInvariant();

        if (errorLower.Contains("fetch") ||
            errorLower.Contains("network") ||
            errorLower.Contains("connection") ||
            errorLower.Contains("failed to fetch"))
        {
            return _localizer["ConnectionError"];
        }

        return message;
    }

    static bool IsTechnicalMessage(string message)
        // Simple heuristic to identify technical messages that shouldn't be shown directly
        => message.Contains("Exception") ||
               message.Contains("Stack trace") ||
               message.Contains("SqlError") ||
               message.Contains("Internal Server Error") ||
               message.StartsWith("{") || // JSON blob
               message.Contains("ApiException");
}
