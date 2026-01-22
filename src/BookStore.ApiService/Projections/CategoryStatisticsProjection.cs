using BookStore.ApiService.Events;
using JasperFx.Events;
using JasperFx.Events.Grouping;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Statistics projection for category book counts
public class CategoryStatistics
{
    public Guid Id { get; set; } // Category ID
    public int BookCount { get; set; }

    // Track which books are counted for idempotency
    public HashSet<Guid> BookIds { get; set; } = [];
}

/// <summary>
/// Multi-stream projection that tracks book counts per category.
/// Uses a custom grouper with LoadManyAsync to route events to both
/// added and removed categories, avoiding N+1 queries.
/// </summary>
public class CategoryStatisticsProjectionBuilder : MultiStreamProjection<CategoryStatistics, Guid>
{
    public CategoryStatisticsProjectionBuilder()
    {
        Options.CacheLimitPerTenant = 1000;

        Identity<CategoryAdded>(x => x.Id);
        CustomGrouping(new CategoryStatisticsGrouper());
    }

    public CategoryStatistics Create(CategoryAdded @event) => new()
    {
        Id = @event.Id,
        BookCount = 0
    };

    public void Apply(Guid id, BookAdded @event, CategoryStatistics state)
    {
        state.Id = id;
        if (@event.CategoryIds.Contains(id))
        {
            _ = state.BookIds.Add(@event.Id);
            state.BookCount = state.BookIds.Count;
        }
    }

    public void Apply(Guid id, BookUpdated @event, CategoryStatistics state)
    {
        state.Id = id;
        if (@event.CategoryIds.Contains(id))
        {
            _ = state.BookIds.Add(@event.Id);
        }
        else
        {
            _ = state.BookIds.Remove(@event.Id);
        }

        state.BookCount = state.BookIds.Count;
    }

    public void Apply(Guid id, BookSoftDeleted @event, CategoryStatistics state)
    {
        state.Id = id;
        _ = state.BookIds.Remove(@event.Id);
        state.BookCount = state.BookIds.Count;
    }

    public void Apply(Guid id, BookRestored @event, CategoryStatistics state)
    {
        state.Id = id;
        _ = state.BookIds.Add(@event.Id);
        state.BookCount = state.BookIds.Count;
    }
}

public class CategoryStatisticsGrouper : IAggregateGrouper<Guid>
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
                case CategoryAdded added:
                    grouping.AddEvent(added.Id, @event);
                    break;

                case BookAdded bookAdded:
                    foreach (var catId in bookAdded.CategoryIds)
                    {
                        grouping.AddEvent(catId, @event);
                    }

                    break;

                case BookUpdated bookUpdated:
                    var newCategories = new HashSet<Guid>(bookUpdated.CategoryIds);

                    foreach (var catId in newCategories)
                    {
                        grouping.AddEvent(catId, @event);
                    }

                    if (bookMap.TryGetValue(@event.StreamId, out var previousBook))
                    {
                        foreach (var previousCatId in previousBook.CategoryIds)
                        {
                            if (!newCategories.Contains(previousCatId))
                            {
                                grouping.AddEvent(previousCatId, @event);
                            }
                        }
                    }

                    break;

                case BookSoftDeleted or BookRestored:
                    if (bookMap.TryGetValue(@event.StreamId, out var existingBook))
                    {
                        foreach (var catId in existingBook.CategoryIds)
                        {
                            grouping.AddEvent(catId, @event);
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
            if (@event.Data is CategoryAdded categoryAdded)
            {
                grouping.AddEvent(categoryAdded.Id, @event);
            }
            else if (@event.Data is BookAdded bookAdded)
            {
                foreach (var catId in bookAdded.CategoryIds)
                {
                    grouping.AddEvent(catId, @event);
                }
            }
        }
    }
}
