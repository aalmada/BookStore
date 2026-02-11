using Microsoft.AspNetCore.Http.HttpResults;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Helper methods for ETag handling in API endpoints
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Generate an ETag from a version number
    /// </summary>
    public static string GenerateETag(long version) => $"\"{version}\"";

    /// <summary>
    /// Check if the If-Match header matches the current ETag
    /// Returns true if the precondition is satisfied (or no If-Match header present)
    /// </summary>
    public static bool CheckIfMatch(HttpContext context, string currentETag)
    {
        var ifMatch = context.Request.Headers["If-Match"].FirstOrDefault();

        // If no If-Match header, precondition is satisfied
        if (string.IsNullOrEmpty(ifMatch))
        {
            return true;
        }

        // Check if the provided ETag matches the current one
        return ifMatch == currentETag || ifMatch == "*";
    }

    /// <summary>
    /// Check if the If-None-Match header matches the current ETag
    /// Returns true if content has NOT been modified (ETag matches)
    /// </summary>
    public static bool CheckIfNoneMatch(HttpContext context, string currentETag)
    {
        var ifNoneMatch = context.Request.Headers["If-None-Match"].FirstOrDefault();

        // If no If-None-Match header, content should be returned
        if (string.IsNullOrEmpty(ifNoneMatch))
        {
            return false;
        }

        // If ETags match, content has not been modified
        return ifNoneMatch == currentETag || ifNoneMatch == "*";
    }

    /// <summary>
    /// Add ETag header to response
    /// </summary>
    public static void AddETagHeader(HttpContext context, string etag) => context.Response.Headers["ETag"] = etag;

    /// <summary>
    /// Create a 304 Not Modified response with ETag
    /// </summary>
    public static StatusCodeHttpResult NotModified(string _) => TypedResults.StatusCode(304);

    /// <summary>
    /// Create a 412 Precondition Failed response
    /// </summary>
    public static ProblemHttpResult PreconditionFailed() => TypedResults.Problem(
            detail: "The resource has been modified since you last retrieved it. Please refresh and try again.",
            statusCode: 412,
            title: "Precondition Failed");

    /// <summary>
    /// Parse an ETag string into a version number
    /// </summary>
    public static long? ParseETag(string? etag)
    {
        if (string.IsNullOrEmpty(etag))
        {
            return null;
        }

        // Strip quotes if present
        var cleanETag = etag.Trim('"');

        // Handle W/ prefix
        if (cleanETag.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            cleanETag = cleanETag[2..].Trim('"');
        }

        return long.TryParse(cleanETag, out var version) ? version : null;
    }
}

public static class ETagResultExtensions
{
    public static IResult WithETag(this IResult result, string etag) => new ETagResult(result, etag);

    sealed class ETagResult(IResult inner, string etag) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.ETag = etag;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
