using BookStore.ApiService.Models;
using BookStore.Shared.Messages.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

public class BookStatisticsProjection : MultiStreamProjection<BookStatistics, Guid>
{
    public BookStatisticsProjection()
    {
        Identity<BookAddedToFavorites>(e => e.BookId);
        Identity<BookRemovedFromFavorites>(e => e.BookId);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public void Apply(BookAddedToFavorites @event, BookStatistics stats) 
        => stats.LikeCount++;

    public void Apply(BookRemovedFromFavorites @event, BookStatistics stats) 
        => stats.LikeCount = Math.Max(0, stats.LikeCount - 1);
#pragma warning restore IDE0060 // Remove unused parameter
}
