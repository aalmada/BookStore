using BookStore.ApiService.Events;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Statistics projection for author book counts
public class AuthorStatistics
{
    public Guid Id { get; set; } // Author ID
    public int BookCount { get; set; }
}

public class AuthorStatisticsProjectionBuilder : MultiStreamProjection<AuthorStatistics, Guid>
{
    public AuthorStatisticsProjectionBuilder()
    {
        // Listen to book events
        Identity<BookAdded>(x => x.Id);
        Identity<BookUpdated>(x => x.Id);
        Identity<BookSoftDeleted>(x => x.Id);
        Identity<BookRestored>(x => x.Id);
        
        // Also listen to author creation to initialize stats
        Identity<AuthorAdded>(x => x.Id);
    }

    // Initialize stats when author is created
    public AuthorStatistics Create(AuthorAdded @event)
    {
        return new AuthorStatistics
        {
            Id = @event.Id,
            BookCount = 0
        };
    }

    // When a book is added, increment count for each author
    public async Task Apply(BookAdded @event, AuthorStatistics projection, IQuerySession session)
    {
        // Check if this author is in the book's author list
        if (@event.AuthorIds.Contains(projection.Id))
        {
            projection.BookCount++;
        }
    }

    // When a book is updated, recalculate if author list changed
    public async Task Apply(BookUpdated @event, AuthorStatistics projection, IQuerySession session)
    {
        // Load the book to check previous state
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null) return;

        // Check if this author was added or removed
        bool wasInBook = book.AuthorIds.Contains(projection.Id);
        bool isInBook = @event.AuthorIds.Contains(projection.Id);

        if (!wasInBook && isInBook)
        {
            // Author was added to book
            projection.BookCount++;
        }
        else if (wasInBook && !isInBook)
        {
            // Author was removed from book
            projection.BookCount = int.Max(0, projection.BookCount - 1);
        }
    }

    // When a book is soft-deleted, decrement count
    public async Task Apply(BookSoftDeleted @event, AuthorStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null) return;

        if (book.AuthorIds.Contains(projection.Id))
        {
            projection.BookCount = int.Max(0, projection.BookCount - 1);
        }
    }

    // When a book is restored, increment count
    public async Task Apply(BookRestored @event, AuthorStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null) return;

        if (book.AuthorIds.Contains(projection.Id))
        {
            projection.BookCount++;
        }
    }
}
