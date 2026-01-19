namespace BookStore.Shared.Models;

/// <summary>
/// Extension methods for <see cref="CoverImageFormat"/>.
/// </summary>
public static class CoverImageFormatExtensions
{
    /// <summary>
    /// Converts the format to a file extension (without dot).
    /// </summary>
    public static string ToExtension(this CoverImageFormat format) => format switch
    {
        CoverImageFormat.Png => "png",
        CoverImageFormat.Jpg => "jpg",
        CoverImageFormat.Webp => "webp",
        CoverImageFormat.None => throw new InvalidOperationException("Cannot get extension for None format"),
        _ => throw new ArgumentException($"Invalid format: {format}", nameof(format))
    };

    /// <summary>
    /// Converts the format to a MIME content type.
    /// </summary>
    public static string ToContentType(this CoverImageFormat format) => format switch
    {
        CoverImageFormat.Png => "image/png",
        CoverImageFormat.Jpg => "image/jpeg",
        CoverImageFormat.Webp => "image/webp",
        CoverImageFormat.None => throw new InvalidOperationException("Cannot get content type for None format"),
        _ => throw new ArgumentException($"Invalid format: {format}", nameof(format))
    };

    /// <summary>
    /// Converts a MIME content type to a <see cref="CoverImageFormat"/>.
    /// Defaults to PNG if the content type is unrecognized.
    /// </summary>
    public static CoverImageFormat FromContentType(string contentType) => contentType switch
    {
        "image/png" => CoverImageFormat.Png,
        "image/jpeg" => CoverImageFormat.Jpg,
        "image/webp" => CoverImageFormat.Webp,
        _ => CoverImageFormat.Png // Default to PNG for unknown types
    };
}
