namespace BookStore.ApiService.Events;

// Book Events - simplified without metadata (Marten handles it)
public record BookAdded(
    Guid Id,
    string Title,
    string? Isbn,
    string? Description,
    DateOnly? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds);

public record BookUpdated(
    Guid Id,
    string Title,
    string? Isbn,
    string? Description,
    DateOnly? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds);

public record BookSoftDeleted(Guid Id);

public record BookRestored(Guid Id);
