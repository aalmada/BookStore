using BookStore.ApiService.Events;
using BookStore.Shared.Models;
using Marten;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

// Read model optimized for search
public class BookSearchProjection
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Isbn { get; set; }
    public string OriginalLanguage { get; set; } = string.Empty;
    public PartialDate? PublicationDate { get; set; }
    public string? PublicationDateString { get; set; } // For generic string sorting (Marten friendly)
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Localized field as dictionary (key = culture, value = description)
    public Dictionary<string, string> Descriptions { get; set; } = [];

    // Prices as list for better query support in Marten
    public List<PriceEntry> Prices { get; set; } = [];

    // Denormalized fields for performance
    public Guid? PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public List<Guid> AuthorIds { get; set; } = [];
    public string AuthorNames { get; set; } = string.Empty; // Concatenated for search
    public List<Guid> CategoryIds { get; set; } = [];

    // Computed search text for ngram matching
    public string SearchText { get; set; } = string.Empty;

    public List<BookSale> Sales { get; set; } = [];

    public CoverImageFormat CoverFormat { get; set; } = CoverImageFormat.None;

    // SingleStreamProjection methods
    public static BookSearchProjection Create(BookAdded @event, IQuerySession session)
    {
        var projection = new BookSearchProjection
        {
            Id = @event.Id,
            Title = @event.Title,
            Isbn = @event.Isbn,
            OriginalLanguage = @event.Language,
            PublicationDate = @event.PublicationDate,
            PublicationDateString = @event.PublicationDate?.ToString(),
            PublisherId = @event.PublisherId,
            AuthorIds = @event.AuthorIds,
            CategoryIds = @event.CategoryIds,
            Descriptions = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Description)
                ?? [],
            Prices = @event.Prices?
                .Select(kvp => new PriceEntry(kvp.Key, kvp.Value))
                .ToList() ?? []
        };

        LoadDenormalizedData(projection, session);
        UpdateSearchText(projection);

        return projection;
    }

    public BookSearchProjection Apply(BookUpdated @event, IQuerySession session)
    {
        Title = @event.Title;
        Isbn = @event.Isbn;
        OriginalLanguage = @event.Language;
        PublicationDate = @event.PublicationDate;
        PublicationDateString = @event.PublicationDate?.ToString();
        PublisherId = @event.PublisherId;
        AuthorIds = @event.AuthorIds;
        CategoryIds = @event.CategoryIds;
        Descriptions = @event.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Description)
            ?? [];
        Prices = @event.Prices?
            .Select(kvp => new PriceEntry(kvp.Key, kvp.Value))
            .ToList() ?? [];

        LoadDenormalizedData(this, session);
        UpdateSearchText(this);

        return this;
    }

    public void Apply(BookSoftDeleted @event)
    {
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    public void Apply(BookRestored _)
    {
        Deleted = false;
        DeletedAt = null;
    }

    public void Apply(BookCoverUpdated @event) => CoverFormat = @event.CoverFormat;

    public void Apply(BookSaleScheduled @event)
    {
        // Remove any existing sale with the same start time
        _ = Sales.RemoveAll(s => s.Start == @event.Sale.Start);
        Sales.Add(@event.Sale);
    }

    public void Apply(BookSaleCancelled @event) => _ = Sales.RemoveAll(s => s.Start == @event.SaleStart);

    // Helper methods for denormalization
    static void LoadDenormalizedData(BookSearchProjection projection, IQuerySession session)
    {
        // Load publisher name
        if (projection.PublisherId.HasValue)
        {
            var publisher = session.Query<PublisherProjection>()
                .FirstOrDefault(p => p.Id == projection.PublisherId.Value);
            projection.PublisherName = publisher?.Name;
        }
        else
        {
            projection.PublisherName = null;
        }

        // Load author names
        if (projection.AuthorIds.Count > 0)
        {
            var authors = session.Query<AuthorProjection>()
                .Where(a => projection.AuthorIds.Contains(a.Id))
                .ToList();
            projection.AuthorNames = string.Join(", ", authors.Select(a => a.Name));
        }
        else
        {
            projection.AuthorNames = string.Empty;
        }
    }

    static void UpdateSearchText(BookSearchProjection projection) => projection.SearchText = $"{projection.Title} {projection.Isbn ?? string.Empty} {projection.PublisherName ?? string.Empty} {projection.AuthorNames}".Trim();
}

public record PriceEntry(string Currency, decimal Value);
