using BookStore.ApiService.Events;
using JasperFx.Events;
using JasperFx.Events.Grouping;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Statistics projection for author book counts
public class AuthorStatistics
{
    public Guid Id { get; set; } // Author ID
    public int BookCount { get; set; }

    // Track which books are counted for idempotency
    public HashSet<Guid> BookIds { get; set; } = [];
}

/// <summary>
/// Multi-stream projection that tracks book counts per author.
/// Uses a custom grouper with LoadManyAsync to route events to both
/// added and removed authors, avoiding N+1 queries.
/// </summary>
public class AuthorStatisticsProjectionBuilder : MultiStreamProjection<AuthorStatistics, Guid>
{
    public AuthorStatisticsProjectionBuilder()
    {
        // Enable caching for batch efficiency
        Options.CacheLimitPerTenant = 1000;

        // Listen to author creation to initialize stats
        Identity<AuthorAdded>(x => x.Id);

        // Listen for book events - routes to multiple authors
        CustomGrouping(new AuthorStatisticsGrouper());
    }

    public AuthorStatistics Create(AuthorAdded @event) => new()
    {
        Id = @event.Id,
        BookCount = 0
    };

    // Use default constructor for other events if state doesn't exist

    public void Apply(Guid id, BookAdded @event, AuthorStatistics state)
    {
        // Ensure state.Id is set (Marten might do it, but we force it for logic)
        state.Id = id;

        if (@event.AuthorIds.Contains(id))
        {
            _ = state.BookIds.Add(@event.Id);
            state.BookCount = state.BookIds.Count;
        }
    }

    public void Apply(Guid id, BookUpdated @event, AuthorStatistics state)
    {
        state.Id = id;
        if (@event.AuthorIds.Contains(id))
        {
            _ = state.BookIds.Add(@event.Id);
        }
        else
        {
            // If routed here but not in the list, it means author was removed
            _ = state.BookIds.Remove(@event.Id);
        }

        state.BookCount = state.BookIds.Count;
    }

    public void Apply(Guid id, BookSoftDeleted @event, AuthorStatistics state)
    {
        state.Id = id;
        _ = state.BookIds.Remove(@event.Id);
        state.BookCount = state.BookIds.Count;
    }

    public void Apply(Guid id, BookRestored @event, AuthorStatistics state)
    {
        state.Id = id;
        _ = state.BookIds.Add(@event.Id);
        state.BookCount = state.BookIds.Count;
    }
}

/// <summary>
/// Custom grouper that routes book events to the correct author statistics documents.
/// Batches lookups of existing book state to determine affected authors (added OR removed).
/// </summary>
public class AuthorStatisticsGrouper : IAggregateGrouper<Guid>
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

        // 1. Batch load previous book states
        var bookIds = bookEvents.Select(e => e.StreamId).Distinct().ToArray();
        var books = await session.LoadManyAsync<BookSearchProjection>(bookIds);
        var bookMap = books.ToDictionary(b => b.Id);

        // 2. Route events
        foreach (var @event in events)
        {
            switch (@event.Data)
            {
                case AuthorAdded added:
                    grouping.AddEvent(added.Id, @event);
                    break;

                case BookAdded bookAdded:
                    foreach (var authorId in bookAdded.AuthorIds)
                    {
                        grouping.AddEvent(authorId, @event);
                    }

                    break;

                case BookUpdated bookUpdated:
                    var newAuthors = new HashSet<Guid>(bookUpdated.AuthorIds);

                    // Route to new authors
                    foreach (var authorId in newAuthors)
                    {
                        grouping.AddEvent(authorId, @event);
                    }

                    // Route to removed authors (diff with previous state)
                    if (bookMap.TryGetValue(@event.StreamId, out var previousBook))
                    {
                        foreach (var previousAuthorId in previousBook.AuthorIds)
                        {
                            if (!newAuthors.Contains(previousAuthorId))
                            {
                                grouping.AddEvent(previousAuthorId, @event);
                            }
                        }
                    }

                    break;

                case BookSoftDeleted or BookRestored:
                    if (bookMap.TryGetValue(@event.StreamId, out var existingBook))
                    {
                        foreach (var authorId in existingBook.AuthorIds)
                        {
                            grouping.AddEvent(authorId, @event);
                        }
                    }

                    break;
            }
        }
    }

    static void RouteSimpleEvents(IEnumerable<IEvent> events, IEventGrouping<Guid> grouping)
    {
        foreach (var @event in events)
        {
            if (@event.Data is AuthorAdded added)
            {
                grouping.AddEvent(added.Id, @event);
            }
            else if (@event.Data is BookAdded bookAdded)
            {
                foreach (var authorId in bookAdded.AuthorIds)
                {
                    grouping.AddEvent(authorId, @event);
                }
            }
        }
    }
}
