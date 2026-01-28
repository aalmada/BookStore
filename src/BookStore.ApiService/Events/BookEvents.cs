using BookStore.Shared.Models;

namespace BookStore.ApiService.Events;

// Book Events - simplified without metadata (Marten handles it)
public record BookAdded(
    Guid Id,
    string Title,
    string? Isbn,
    string Language,
    Dictionary<string, BookTranslation> Translations,
    PartialDate? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds,
    Dictionary<string, decimal> Prices);

public record BookUpdated(
    Guid Id,
    string Title,
    string? Isbn,
    string Language,
    Dictionary<string, BookTranslation> Translations,
    PartialDate? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds,
    Dictionary<string, decimal> Prices);

public record BookSoftDeleted(Guid Id, DateTimeOffset Timestamp);

public record BookRestored(Guid Id, DateTimeOffset Timestamp);

public record BookCoverUpdated(Guid Id, CoverImageFormat CoverFormat);

public record BookSaleScheduled(Guid Id, BookSale Sale);

public record BookSaleCancelled(Guid Id, DateTimeOffset SaleStart);

public record BookDiscountUpdated(Guid Id, decimal DiscountPercentage);


// Localization model for book descriptions
public record BookTranslation(string Description);

