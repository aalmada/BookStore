using BookStore.ApiService.Events;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Statistics projection for category book counts
public class CategoryStatistics
{
    public Guid Id { get; set; } // Category ID
    public int BookCount { get; set; }
}

public class CategoryStatisticsProjectionBuilder : MultiStreamProjection<CategoryStatistics, Guid>
{
    public CategoryStatisticsProjectionBuilder()
    {
        // Listen to book events
        Identity<BookAdded>(x => x.Id);
        Identity<BookUpdated>(x => x.Id);
        Identity<BookSoftDeleted>(x => x.Id);
        Identity<BookRestored>(x => x.Id);

        // Also listen to category creation to initialize stats
        Identity<CategoryAdded>(x => x.Id);
    }

    // Initialize stats when category is created
    public CategoryStatistics Create(CategoryAdded @event) => new()
    {
        Id = @event.Id,
        BookCount = 0
    };

    // When a book is added, increment count for each category
    public async Task Apply(BookAdded @event, CategoryStatistics projection, IQuerySession session)
    {
        if (@event.CategoryIds.Contains(projection.Id))
        {
            projection.BookCount++;
        }
    }

    // When a book is updated, recalculate if category list changed
    public async Task Apply(BookUpdated @event, CategoryStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null)
        {
            return;
        }

        var wasInBook = book.CategoryIds.Contains(projection.Id);
        var isInBook = @event.CategoryIds.Contains(projection.Id);

        if (!wasInBook && isInBook)
        {
            projection.BookCount++;
        }
        else if (wasInBook && !isInBook)
        {
            projection.BookCount = int.Max(0, projection.BookCount - 1);
        }
    }

    // When a book is soft-deleted, decrement count
    public async Task Apply(BookSoftDeleted @event, CategoryStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null)
        {
            return;
        }

        if (book.CategoryIds.Contains(projection.Id))
        {
            projection.BookCount = int.Max(0, projection.BookCount - 1);
        }
    }

    // When a book is restored, increment count
    public async Task Apply(BookRestored @event, CategoryStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null)
        {
            return;
        }

        if (book.CategoryIds.Contains(projection.Id))
        {
            projection.BookCount++;
        }
    }
}
