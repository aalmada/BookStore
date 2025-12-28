using BookStore.ApiService.Events;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Statistics projection for publisher book counts
public class PublisherStatistics
{
    public Guid Id { get; set; } // Publisher ID
    public int BookCount { get; set; }
}

public class PublisherStatisticsProjectionBuilder : MultiStreamProjection<PublisherStatistics, Guid>
{
    public PublisherStatisticsProjectionBuilder()
    {
        // Listen to book events
        Identity<BookAdded>(x => x.Id);
        Identity<BookUpdated>(x => x.Id);
        Identity<BookSoftDeleted>(x => x.Id);
        Identity<BookRestored>(x => x.Id);

        // Also listen to publisher creation to initialize stats
        Identity<PublisherAdded>(x => x.Id);
    }

    // Initialize stats when publisher is created
    public PublisherStatistics Create(PublisherAdded @event) => new()
    {
        Id = @event.Id,
        BookCount = 0
    };

    // When a book is added, increment count if it uses this publisher
    public async Task Apply(BookAdded @event, PublisherStatistics projection, IQuerySession session)
    {
        if (@event.PublisherId == projection.Id)
        {
            projection.BookCount++;
        }
    }

    // When a book is updated, recalculate if publisher changed
    public async Task Apply(BookUpdated @event, PublisherStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null)
        {
            return;
        }

        var wasPublisher = book.PublisherId == projection.Id;
        var isPublisher = @event.PublisherId == projection.Id;

        if (!wasPublisher && isPublisher)
        {
            projection.BookCount++;
        }
        else if (wasPublisher && !isPublisher)
        {
            projection.BookCount = int.Max(0, projection.BookCount - 1);
        }
    }

    // When a book is soft-deleted, decrement count
    public async Task Apply(BookSoftDeleted @event, PublisherStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null)
        {
            return;
        }

        if (book.PublisherId == projection.Id)
        {
            projection.BookCount = int.Max(0, projection.BookCount - 1);
        }
    }

    // When a book is restored, increment count
    public async Task Apply(BookRestored @event, PublisherStatistics projection, IQuerySession session)
    {
        var book = await session.LoadAsync<BookSearchProjection>(@event.Id);
        if (book == null)
        {
            return;
        }

        if (book.PublisherId == projection.Id)
        {
            projection.BookCount++;
        }
    }
}
