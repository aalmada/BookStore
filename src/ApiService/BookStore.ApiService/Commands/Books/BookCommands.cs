namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to create a new book
/// </summary>
public record CreateBook(
    string Title,
    string? Isbn,
    string? Description,
    DateOnly? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds)
{
    /// <summary>
    /// Unique identifier for the book (generated automatically)
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// Command to update an existing book
/// </summary>
public record UpdateBook(
    Guid Id,
    string Title,
    string? Isbn,
    string? Description,
    DateOnly? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds,
    List<Guid> CategoryIds)
{
    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Command to soft delete a book
/// </summary>
public record SoftDeleteBook(Guid Id)
{
    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Command to restore a soft deleted book
/// </summary>
public record RestoreBook(Guid Id)
{
    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Command to update a book's cover image
/// </summary>
public record UpdateBookCover(
    Guid BookId,
    Stream ImageStream,
    string ContentType)
{
    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public string? ETag { get; init; }
}
