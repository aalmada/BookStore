using BookStore.ApiService.Events;
using Marten;

namespace BookStore.ApiService.Aggregates;

public class BookAggregate
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Isbn { get; set; }
    public string? Description { get; set; }
    public DateOnly? PublicationDate { get; set; }
    public Guid? PublisherId { get; set; }
    public List<Guid> AuthorIds { get; set; } = [];
    public List<Guid> CategoryIds { get; set; } = [];
    public bool IsDeleted { get; set; }

    // Marten uses this for rehydration
    void Apply(BookAdded @event)
    {
        Id = @event.Id;
        Title = @event.Title;
        Isbn = @event.Isbn;
        Description = @event.Description;
        PublicationDate = @event.PublicationDate;
        PublisherId = @event.PublisherId;
        AuthorIds = @event.AuthorIds;
        CategoryIds = @event.CategoryIds;
        IsDeleted = false;
    }

    void Apply(BookUpdated @event)
    {
        Title = @event.Title;
        Isbn = @event.Isbn;
        Description = @event.Description;
        PublicationDate = @event.PublicationDate;
        PublisherId = @event.PublisherId;
        AuthorIds = @event.AuthorIds;
        CategoryIds = @event.CategoryIds;
    }

    void Apply(BookSoftDeleted @event)
    {
        IsDeleted = true;
    }

    void Apply(BookRestored @event)
    {
        IsDeleted = false;
    }

    // Command methods
    public static BookAdded Create(
        Guid id,
        string title,
        string? isbn,
        string? description,
        DateOnly? publicationDate,
        Guid? publisherId,
        List<Guid> authorIds,
        List<Guid> categoryIds)
    {
        // Validate all inputs before creating event
        ValidateTitle(title);
        ValidateIsbn(isbn);
        ValidateDescription(description);
        ValidatePublicationDate(publicationDate);

        return new BookAdded(
            id,
            title,
            isbn,
            description,
            publicationDate,
            publisherId,
            authorIds,
            categoryIds);
    }

    public BookUpdated Update(
        string title,
        string? isbn,
        string? description,
        DateOnly? publicationDate,
        Guid? publisherId,
        List<Guid> authorIds,
        List<Guid> categoryIds)
    {
        // Business rule: cannot update deleted book
        if (IsDeleted)
            throw new InvalidOperationException("Cannot update a deleted book");

        // Validate all inputs before creating event
        ValidateTitle(title);
        ValidateIsbn(isbn);
        ValidateDescription(description);
        ValidatePublicationDate(publicationDate);

        return new BookUpdated(
            Id,
            title,
            isbn,
            description,
            publicationDate,
            publisherId,
            authorIds,
            categoryIds);
    }

    // Validation helper methods
    static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (title.Length > 500)
            throw new ArgumentException("Title cannot exceed 500 characters", nameof(title));
    }

    static void ValidateIsbn(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return; // ISBN is optional

        // Remove hyphens and spaces for validation
        var cleanIsbn = new string(isbn.Where(char.IsDigit).ToArray());

        // ISBN-10 or ISBN-13
        if (cleanIsbn.Length != 10 && cleanIsbn.Length != 13)
            throw new ArgumentException("ISBN must be 10 or 13 digits", nameof(isbn));
    }

    static void ValidateDescription(string? description)
    {
        if (description != null && description.Length > 5000)
            throw new ArgumentException("Description cannot exceed 5000 characters", nameof(description));
    }

    static void ValidatePublicationDate(DateOnly? publicationDate)
    {
        if (publicationDate.HasValue && publicationDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Publication date cannot be in the future", nameof(publicationDate));
    }

    public BookSoftDeleted SoftDelete()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Book is already deleted");

        return new BookSoftDeleted(Id);
    }

    public BookRestored Restore()
    {
        if (!IsDeleted)
            throw new InvalidOperationException("Book is not deleted");

        return new BookRestored(Id);
    }
}
