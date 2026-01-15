namespace BookStore.ApiService.Models;

public class BookStatistics
{
    public Guid Id { get; set; }
    public int LikeCount { get; set; }

    /// <summary>
    /// Individual user ratings (UserId -> Rating 1-5)
    /// Used to efficiently handle rating updates and removals
    /// </summary>
    public IDictionary<Guid, int> UserRatings { get; set; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Total sum of all rating values
    /// </summary>
    public int TotalRatingScore { get; set; }

    /// <summary>
    /// Number of users who have rated this book
    /// </summary>
    public int RatingCount { get; set; }

    /// <summary>
    /// Average rating (calculated from TotalRatingScore / RatingCount)
    /// </summary>
    public float AverageRating { get; set; }
}
