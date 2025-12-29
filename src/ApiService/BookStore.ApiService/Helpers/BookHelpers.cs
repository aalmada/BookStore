using BookStore.ApiService.Models;

namespace BookStore.ApiService.Helpers;

/// <summary>
/// Helper methods for book-related operations
/// </summary>
internal static class BookHelpers
{
    /// <summary>
    /// Determines if a book is a pre-release (publication date in the future)
    /// </summary>
    public static bool IsPreRelease(PartialDate? publicationDate)
    {
        if (!publicationDate.HasValue)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
        var todayPartial = new PartialDate(today.Year, today.Month, today.Day);

        return publicationDate.Value > todayPartial;
    }
}
