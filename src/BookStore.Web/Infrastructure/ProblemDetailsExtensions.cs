using System.Net;
using System.Text.Json;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.Web.Infrastructure;

public static class ProblemDetailsExtensions
{
    public static Error ToError(this ApiException exception)
    {
        if (string.IsNullOrEmpty(exception.Content))
        {
            return Error.Failure("ERR_HTTP_FAILURE", $"Request failed with status {exception.StatusCode}");
        }

        try
        {
            using var doc = JsonDocument.Parse(exception.Content);

            // Try to get "code" from extensions first (standard for our new pattern)
            string? code = null;
            if (doc.RootElement.TryGetProperty("extensions", out var extensions) &&
                extensions.TryGetProperty("code", out var codeProp))
            {
                code = codeProp.GetString();
            }

            // Fallback: check if "code" is at the root (unlikely but possible)
            code ??= doc.RootElement.TryGetProperty("code", out var rootCode) ? rootCode.GetString() : null;

            // Default code if still null
            code ??= $"ERR_HTTP_{exception.StatusCode.ToString().ToUpperInvariant()}";

            // Try to get "detail" from ProblemDetails
            string? message = null;
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                message = detail.GetString();
            }

            // Fallback: Check for "errors" array (Identity style)
            if (string.IsNullOrEmpty(message) &&
                doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                var firstError = errors[0];
                if (firstError.TryGetProperty("description", out var desc))
                {
                    message = desc.GetString();
                }
            }

            // Fallback: Check if it's a simple string array (old style or some internal APIs)
            if (string.IsNullOrEmpty(message) &&
                doc.RootElement.ValueKind == JsonValueKind.Array &&
                doc.RootElement.GetArrayLength() > 0)
            {
                var firstItem = doc.RootElement[0];
                if (firstItem.TryGetProperty("description", out var desc))
                {
                    message = desc.GetString();
                }
            }

            message ??= "An unexpected error occurred.";

            return exception.StatusCode switch
            {
                HttpStatusCode.BadRequest => Error.Validation(code, message),
                HttpStatusCode.NotFound => Error.NotFound(code, message),
                HttpStatusCode.Unauthorized => Error.Unauthorized(code, message),
                HttpStatusCode.Forbidden => Error.Forbidden(code, message),
                HttpStatusCode.Conflict => Error.Conflict(code, message),
                HttpStatusCode.InternalServerError => Error.InternalServerError(code, message),
                _ => Error.Failure(code, message)
            };
        }
        catch
        {
            // Fallback for non-JSON content
            var fallbackMessage = exception.Content.Trim('"');
            return Error.Failure($"ERR_HTTP_{exception.StatusCode}", fallbackMessage);
        }
    }

    public static Result ToResult(this ApiException exception) => Result.Failure(exception.ToError());

    public static Result<T> ToResult<T>(this ApiException exception) => Result.Failure<T>(exception.ToError());
}
