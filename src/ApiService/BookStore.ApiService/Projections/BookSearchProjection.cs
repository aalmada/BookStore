using BookStore.ApiService.Events;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace BookStore.ApiService.Projections;

// Read model optimized for search
public class BookSearchProjection
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Isbn { get; set; }
    public string? Description { get; set; }
    public DateOnly? PublicationDate { get; set; }

    // Denormalized fields for performance
    public Guid? PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public List<Guid> AuthorIds { get; set; } = [];
    public string AuthorNames { get; set; } = string.Empty; // Concatenated for search
    public List<Guid> CategoryIds { get; set; } = []; // For filtering by ID only (categories are localizable)

    // Computed search text for ngram matching
    public string SearchText { get; set; } = string.Empty;
}

// Multi-stream async projection
public class BookSearchProjectionBuilder : MultiStreamProjection<BookSearchProjection, Guid>
{
    public BookSearchProjectionBuilder()
    {
        // This projection listens to multiple stream types
        Identity<BookAdded>(x => x.Id);
        Identity<BookUpdated>(x => x.Id);
        Identity<BookSoftDeleted>(x => x.Id);
        Identity<BookRestored>(x => x.Id);

        // Delete projection when book is soft-deleted
        _ = DeleteEvent<BookSoftDeleted>();

        // Also listen to publisher events to update denormalized data
        Identity<PublisherUpdated>(x => x.Id);
        Identity<PublisherSoftDeleted>(x => x.Id);

        // And author events
        Identity<AuthorUpdated>(x => x.Id);
        Identity<AuthorSoftDeleted>(x => x.Id);
    }

    public async Task<BookSearchProjection> Create(BookAdded @event, IQuerySession session)
    {
        var projection = new BookSearchProjection
        {
            Id = @event.Id,
            Title = @event.Title,
            Isbn = @event.Isbn,
            Description = @event.Description,
            PublicationDate = @event.PublicationDate,
            PublisherId = @event.PublisherId,
            AuthorIds = @event.AuthorIds,
            CategoryIds = @event.CategoryIds
        };

        // Populate publisher name
        if (projection.PublisherId.HasValue)
        {
            var publisher = await session.LoadAsync<PublisherProjection>(projection.PublisherId.Value);
            projection.PublisherName = publisher?.Name;
        }

        // Populate author names
        if (projection.AuthorIds.Any())
        {
            var authors = await session.Query<AuthorProjection>()
                .Where(a => projection.AuthorIds.Contains(a.Id))
                .ToListAsync();
            projection.AuthorNames = string.Join(", ", authors.Select(a => a.Name));
        }

        // Compute SearchText
        UpdateSearchText(projection);

        return projection;
    }

    public async Task Apply(BookUpdated @event, BookSearchProjection projection, IQuerySession session)
    {
        projection.Title = @event.Title;
        projection.Isbn = @event.Isbn;
        projection.Description = @event.Description;
        projection.PublicationDate = @event.PublicationDate;
        projection.PublisherId = @event.PublisherId;
        projection.AuthorIds = @event.AuthorIds;
        projection.CategoryIds = @event.CategoryIds;

        // Re-populate publisher name (may have changed)
        if (projection.PublisherId.HasValue)
        {
            var publisher = await session.LoadAsync<PublisherProjection>(projection.PublisherId.Value);
            projection.PublisherName = publisher?.Name;
        }
        else
        {
            projection.PublisherName = null;
        }

        // Re-populate author names (IDs may have changed)
        if (projection.AuthorIds.Any())
        {
            var authors = await session.Query<AuthorProjection>()
                .Where(a => projection.AuthorIds.Contains(a.Id))
                .ToListAsync();
            projection.AuthorNames = string.Join(", ", authors.Select(a => a.Name));
        }
        else
        {
            projection.AuthorNames = string.Empty;
        }

        // Update SearchText (title, description, ISBN, publisher, authors may have changed)
        UpdateSearchText(projection);
    }

    void Apply(PublisherUpdated @event, BookSearchProjection projection)
    {
        // Update publisher name directly from event (no query needed!)
        if (projection.PublisherId == @event.Id)
        {
            projection.PublisherName = @event.Name;
            UpdateSearchText(projection);
        }
    }

    public async Task Apply(AuthorUpdated @event, BookSearchProjection projection, IQuerySession session)
    {
        // Re-populate author names if this book has the updated author
        if (projection.AuthorIds.Contains(@event.Id))
        {
            var authors = await session.Query<AuthorProjection>()
                .Where(a => projection.AuthorIds.Contains(a.Id))
                .ToListAsync();
            projection.AuthorNames = string.Join(", ", authors.Select(a => a.Name));

            // Update SearchText (author names changed)
            UpdateSearchText(projection);
        }
    }

    // Helper method to compute SearchText from all searchable fields
    void UpdateSearchText(BookSearchProjection projection)
        // Use string interpolation to avoid List<string> allocation
        => projection.SearchText =
            $"{projection.Title} " +
            $"{projection.Description ?? string.Empty} " +
            $"{projection.Isbn ?? string.Empty} " +
            $"{projection.PublisherName ?? string.Empty} " +
            $"{projection.AuthorNames}".Trim();

    // Projection will be deleted on BookSoftDeleted (configured in constructor)
    // Projection will be recreated on BookRestored by replaying the stream

    // Note: Publisher/Author/Category updates would require querying the database
    // to update denormalized fields. This will be handled in the Program.cs configuration
    // using custom projection logic or separate projections.
}
