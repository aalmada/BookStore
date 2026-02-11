namespace BookStore.Client;

/// <summary>
/// Helper methods for ETag handling in the client
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Generate an ETag string from a version number
    /// </summary>
    /// <param name="version">The version number</param>
    /// <returns>ETag string in the format "{version}"</returns>
    public static string GenerateETag(long version) => $"\"{version}\"";

    /// <summary>
    /// Parse an ETag string into a version number
    /// </summary>
    /// <param name="etag">The ETag string to parse</param>
    /// <returns>The version number, or null if parsing fails</returns>
    public static long? ParseETag(string? etag)
    {
        if (string.IsNullOrEmpty(etag))
        {
            return null;
        }

        // Strip quotes if present
        var cleanETag = etag.Trim('"');

        // Handle W/ prefix for weak ETags
        if (cleanETag.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            cleanETag = cleanETag[2..].Trim('"');
        }

        return long.TryParse(cleanETag, out var version) ? version : null;
    }

    /// <summary>
    /// Try to parse an ETag string into a version number
    /// </summary>
    /// <param name="etag">The ETag string to parse</param>
    /// <param name="version">The parsed version number</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParseETag(string? etag, out long version)
    {
        var result = ParseETag(etag);
        if (result.HasValue)
        {
            version = result.Value;
            return true;
        }

        version = 0;
        return false;
    }
}
