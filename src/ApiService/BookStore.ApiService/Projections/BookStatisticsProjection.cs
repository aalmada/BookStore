using BookStore.ApiService.Models;
using BookStore.Shared.Messages.Events;
using JasperFx.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

public class BookStatisticsProjection : MultiStreamProjection<BookStatistics, Guid>
{
    public BookStatisticsProjection()
    {
        Identity<BookAddedToFavorites>(e => e.BookId);
        Identity<BookRemovedFromFavorites>(e => e.BookId);
        Identity<BookRated>(e => e.BookId);
        Identity<BookRatingRemoved>(e => e.BookId);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public void Apply(BookAddedToFavorites @event, BookStatistics stats)
        => stats.LikeCount++;

    public void Apply(BookRemovedFromFavorites @event, BookStatistics stats)
        => stats.LikeCount = int.Max(0, stats.LikeCount - 1);

    public void Apply(IEvent<BookRated> eventEnvelope, BookStatistics stats)
    {
        var @event = eventEnvelope.Data;
        var userId = eventEnvelope.StreamId; // User ID from the event stream

        // Get previous rating if it exists
        var hadPreviousRating = stats.UserRatings.TryGetValue(userId, out var previousRating);

        // Update user's rating
        stats.UserRatings[userId] = @event.Rating;

        // Update totals
        if (hadPreviousRating)
        {
            // User is updating their rating
            stats.TotalRatingScore += (@event.Rating - previousRating);
        }
        else
        {
            // New rating
            stats.TotalRatingScore += @event.Rating;
            stats.RatingCount++;
        }

        // Recalculate average
        stats.AverageRating = stats.RatingCount > 0
            ? (float)stats.TotalRatingScore / stats.RatingCount
            : 0f;
    }

    public void Apply(IEvent<BookRatingRemoved> eventEnvelope, BookStatistics stats)
    {
        var userId = eventEnvelope.StreamId; // User ID from the event stream

        // Remove user's rating if it exists
        if (stats.UserRatings.Remove(userId, out var previousRating))
        {
            stats.TotalRatingScore -= previousRating;
            stats.RatingCount = int.Max(0, stats.RatingCount - 1);

            // Recalculate average
            stats.AverageRating = stats.RatingCount > 0
                ? (float)stats.TotalRatingScore / stats.RatingCount
                : 0f;
        }
    }
#pragma warning restore IDE0060 // Remove unused parameter
}
