namespace BookStore.Shared.Models;

/// <summary>
/// Represents the format of a book cover image.
/// </summary>
public enum CoverImageFormat
{
    /// <summary>
    /// No cover image available.
    /// </summary>
    None = 0,

    /// <summary>
    /// PNG format.
    /// </summary>
    Png = 1,

    /// <summary>
    /// JPEG format.
    /// </summary>
    Jpg = 2,

    /// <summary>
    /// WebP format.
    /// </summary>
    Webp = 3
}
