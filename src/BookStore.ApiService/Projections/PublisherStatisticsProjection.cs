using BookStore.ApiService.Events;
using JasperFx.Events;
using JasperFx.Events.Grouping;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Statistics projection for publisher book counts
public class PublisherStatistics
{
    public Guid Id { get; set; } // Publisher ID
    public int BookCount { get; set; }

    // Track which books are counted for idempotency
    public HashSet<Guid> BookIds { get; set; } = [];
}

/// <summary>
/// Multi-stream projection that tracks book counts per publisher.
/// Uses a custom grouper with LoadManyAsync to route events to both
/// added and removed publishers, avoiding N+1 queries.
/// </summary>
public class PublisherStatisticsProjectionBuilder : MultiStreamProjection<PublisherStatistics, Guid>
{
    public PublisherStatisticsProjectionBuilder()
    {
        Options.CacheLimitPerTenant = 1000;

        Identity<PublisherAdded>(x => x.Id);
        CustomGrouping(new PublisherStatisticsGrouper());
    }

    public PublisherStatistics Create(PublisherAdded @event) => new()
    {
        Id = @event.Id,
        BookCount = 0
    };

    public void Apply(Guid id, BookAdded @event, PublisherStatistics state)
    {
        state.Id = id;
        if (@event.PublisherId == id)
        {
            _ = state.BookIds.Add(@event.Id);
            state.BookCount = state.BookIds.Count;
        }
    }

    public void Apply(Guid id, BookUpdated @event, PublisherStatistics state)
    {
        state.Id = id;
        if (@event.PublisherId == id)
        {
            _ = state.BookIds.Add(@event.Id);
        }
        else
        {
            // If routed here but currently not the publisher, it means it was removed
            _ = state.BookIds.Remove(@event.Id);
        }

        state.BookCount = state.BookIds.Count;
    }

    public void Apply(Guid id, BookSoftDeleted @event, PublisherStatistics state)
    {
        state.Id = id;
        _ = state.BookIds.Remove(@event.Id);
        state.BookCount = state.BookIds.Count;
    }

    public void Apply(Guid id, BookRestored @event, PublisherStatistics state)
    {
        state.Id = id;
        _ = state.BookIds.Add(@event.Id);
        state.BookCount = state.BookIds.Count;
    }
}

public class PublisherStatisticsGrouper : IAggregateGrouper<Guid>
{
    public async Task Group(
        IQuerySession session,
        IEnumerable<IEvent> events,
        IEventGrouping<Guid> grouping)
    {
        var bookEvents = events
            .Where(e => e.Data is BookUpdated or BookSoftDeleted or BookRestored)
            .ToList();

        if (bookEvents.Count == 0)
        {
            RouteSimpleEvents(events, grouping);
            return;
        }

        var bookIds = bookEvents.Select(e => e.StreamId).Distinct().ToArray();
        var books = await session.LoadManyAsync<BookSearchProjection>(bookIds);
        var bookMap = books.ToDictionary(b => b.Id);

        foreach (var @event in events)
        {
            switch (@event.Data)
            {
                case PublisherAdded added:
                    grouping.AddEvent(added.Id, @event);
                    break;

                case BookAdded bookAdded:
                    if (bookAdded.PublisherId.HasValue)
                    {
                        grouping.AddEvent(bookAdded.PublisherId.Value, @event);
                    }

                    break;

                case BookUpdated bookUpdated:
                    // Route to new publisher
                    if (bookUpdated.PublisherId.HasValue)
                    {
                        grouping.AddEvent(bookUpdated.PublisherId.Value, @event);
                    }

                    // Route to old publisher if different
                    if (bookMap.TryGetValue(@event.StreamId, out var previousBook) && previousBook.PublisherId.HasValue)
                    {
                        if (previousBook.PublisherId != bookUpdated.PublisherId)
                        {
                            grouping.AddEvent(previousBook.PublisherId.Value, @event);
                        }
                    }

                    break;

                case BookSoftDeleted or BookRestored:
                    if (bookMap.TryGetValue(@event.StreamId, out var existingBook) && existingBook.PublisherId.HasValue)
                    {
                        grouping.AddEvent(existingBook.PublisherId.Value, @event);
                    }

                    break;
            }
        }
    }

    static void RouteSimpleEvents(IEnumerable<IEvent> events, IEventGrouping<Guid> grouping)
    {
        foreach (var @event in events)
        {
            if (@event.Data is PublisherAdded publisherAdded)
            {
                grouping.AddEvent(publisherAdded.Id, @event);
            }
            else if (@event.Data is BookAdded bookAdded && bookAdded.PublisherId.HasValue)
            {
                grouping.AddEvent(bookAdded.PublisherId.Value, @event);
            }
        }
    }
}
