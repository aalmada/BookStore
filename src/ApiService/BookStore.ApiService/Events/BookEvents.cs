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
    List<Guid> CategoryIds);

public record BookUpdated(
    Guid Id,
    string Title,
    string? Isbn,
    string Language,
    Dictionary<string, BookTranslation> Translations,
    PartialDate? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds);

public record BookSoftDeleted(Guid Id);

public record BookRestored(Guid Id);

public record BookCoverUpdated(Guid Id, string CoverImageUrl);

// Localization model for book descriptions
public record BookTranslation(string Description);
