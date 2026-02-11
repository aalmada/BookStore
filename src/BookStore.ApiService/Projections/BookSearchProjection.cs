using BookStore.ApiService.Events;
using BookStore.Shared.Models;
using JasperFx.Events;
using Marten;
using Marten.Events;
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
    public long Version { get; set; }

    // Localized field as dictionary (key = culture, value = description)
    public Dictionary<string, string> Descriptions { get; set; } = [];

    // Prices as list for better query support in Marten
    public List<PriceEntry> Prices { get; set; } = [];
    public List<PriceEntry> CurrentPrices { get; set; } = [];

    // Denormalized fields for performance
    public Guid? PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public List<Guid> AuthorIds { get; set; } = [];
    public string AuthorNames { get; set; } = string.Empty; // Concatenated for search
    public List<Guid> CategoryIds { get; set; } = [];

    // Computed search text for ngram matching
    public string SearchText { get; set; } = string.Empty;

    public List<BookSale> Sales { get; set; } = [];
    public decimal DiscountPercentage { get; set; }

    public CoverImageFormat CoverFormat { get; set; } = CoverImageFormat.None;

    // SingleStreamProjection methods
    public static BookSearchProjection Create(IEvent<BookAdded> @event, IQuerySession session)
    {
        var prices = @event.Data.Prices?
                .Select(kvp => new PriceEntry(kvp.Key, kvp.Value))
                .ToList() ?? [];

        var projection = new BookSearchProjection
        {
            Id = @event.Data.Id,
            Title = @event.Data.Title,
            Isbn = @event.Data.Isbn,
            OriginalLanguage = @event.Data.Language,
            PublicationDate = @event.Data.PublicationDate,
            PublicationDateString = @event.Data.PublicationDate?.ToString(),
            Version = @event.Version,
            PublisherId = @event.Data.PublisherId,
            AuthorIds = @event.Data.AuthorIds,
            CategoryIds = @event.Data.CategoryIds,
            Descriptions = @event.Data.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Description)
                ?? [],
            Prices = prices,
            CurrentPrices = prices,
            DiscountPercentage = 0
        };

        projection.RecalculateCurrentPrices();
        LoadDenormalizedData(projection, session);
        UpdateSearchText(projection);

        return projection;
    }

    public BookSearchProjection Apply(IEvent<BookUpdated> @event, IQuerySession session)
    {
        Title = @event.Data.Title;
        Isbn = @event.Data.Isbn;
        OriginalLanguage = @event.Data.Language;
        PublicationDate = @event.Data.PublicationDate;
        PublicationDateString = @event.Data.PublicationDate?.ToString();
        Version = @event.Version;
        PublisherId = @event.Data.PublisherId;
        AuthorIds = @event.Data.AuthorIds;
        CategoryIds = @event.Data.CategoryIds;
        Descriptions = @event.Data.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Description)
            ?? [];
        Prices = @event.Data.Prices?
            .Select(kvp => new PriceEntry(kvp.Key, kvp.Value))
            .ToList() ?? [];

        RecalculateCurrentPrices();

        LoadDenormalizedData(this, session);
        UpdateSearchText(this);

        return this;
    }

    // New handler for discount updates
    public void Apply(IEvent<BookDiscountUpdated> @event)
    {
        DiscountPercentage = @event.Data.DiscountPercentage;
        Version = @event.Version;
        RecalculateCurrentPrices();
    }

    void RecalculateCurrentPrices()
    {
        if (Prices == null)
        {
            return;
        }

        var factor = 1 - (DiscountPercentage / 100m);
        CurrentPrices = [.. Prices.Select(p => new PriceEntry(p.Currency, p.Value * factor))];
    }

    public void Apply(IEvent<BookSoftDeleted> @event)
    {
        Deleted = true;
        Version = @event.Version;
        DeletedAt = @event.Timestamp;
    }

    public void Apply(IEvent<BookRestored> @event)
    {
        Deleted = false;
        Version = @event.Version;
        DeletedAt = null;
    }

    public void Apply(IEvent<BookCoverUpdated> @event)
    {
        CoverFormat = @event.Data.CoverFormat;
        Version = @event.Version;
    }

    public void Apply(IEvent<BookSaleScheduled> @event)
    {
        // Legacy: We keep this for backward compatibility or display purposes?
        // Ideally we should sync Sales from SaleAggregate, but BookSearchProjection only sees Book events.
        // If we want 'Sales' in Read Model, we need to read from SaleAggregate events too?
        // Or we rely on 'CurrentPrices' for the effective price and don't care about displaying usage of 'Sale' object in search list.
        // The list view usually shows "On Sale" badge. We need to know if it's on sale.
        // 'CurrentPrices' < 'Prices' implies sale.

        // Remove any existing sale with the same start time
        _ = Sales.RemoveAll(s => s.Start == @event.Data.Sale.Start);
        Sales.Add(@event.Data.Sale);

        // If the sale is currently active, update the discount percentage and recalculate prices
        var now = DateTimeOffset.UtcNow;
        if (@event.Data.Sale.Start <= now && @event.Data.Sale.End > now)
        {
            DiscountPercentage = @event.Data.Sale.Percentage;
        }

        RecalculateCurrentPrices();
        Version = @event.Version;
    }

    public void Apply(IEvent<BookSaleCancelled> @event)
    {
        _ = Sales.RemoveAll(s => s.Start == @event.Data.SaleStart);
        Version = @event.Version;
    }

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

// PriceEntry moved to Shared
