using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Models;
using Marten;

namespace BookStore.ApiService.Aggregates;

public class BookAggregate
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Isbn { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public Dictionary<string, BookTranslation> Translations { get; private set; } = [];
    public PartialDate? PublicationDate { get; private set; }
    public Guid? PublisherId { get; private set; }
    public List<Guid> AuthorIds { get; private set; } = [];
    public List<Guid> CategoryIds { get; private set; } = [];
    public bool IsDeleted { get; private set; }
    public string? CoverImageUrl { get; private set; }

    // Marten uses this for rehydration
    void Apply(BookAdded @event)
    {
        Id = @event.Id;
        Title = @event.Title;
        Isbn = @event.Isbn;
        Language = @event.Language;
        Translations = @event.Translations ?? [];
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
        Language = @event.Language;
        Translations = @event.Translations ?? [];
        PublicationDate = @event.PublicationDate;
        PublisherId = @event.PublisherId;
        AuthorIds = @event.AuthorIds;
        CategoryIds = @event.CategoryIds;
    }

    void Apply(BookSoftDeleted _) => IsDeleted = true;

    void Apply(BookRestored _) => IsDeleted = false;

    void Apply(BookCoverUpdated @event) => CoverImageUrl = @event.CoverImageUrl;

    // Command methods
    public static BookAdded Create(
        Guid id,
        string title,
        string? isbn,
        string language,
        Dictionary<string, BookTranslation>? translations,
        PartialDate? publicationDate,
        Guid? publisherId,
        List<Guid> authorIds,
        List<Guid> categoryIds)
    {
        // Validate all inputs before creating event
        ValidateTitle(title);
        ValidateIsbn(isbn);
        ValidateLanguage(language);
        ValidateTranslations(translations);

        return new BookAdded(
            id,
            title,
            isbn,
            language,
            translations,
            publicationDate,
            publisherId,
            authorIds,
            categoryIds);
    }

    public BookUpdated Update(
        string title,
        string? isbn,
        string language,
        Dictionary<string, BookTranslation>? translations,
        PartialDate? publicationDate,
        Guid? publisherId,
        List<Guid> authorIds,
        List<Guid> categoryIds)
    {
        // Business rule: cannot update deleted book
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted book");
        }

        // Validate all inputs before creating event
        ValidateTitle(title);
        ValidateIsbn(isbn);
        ValidateLanguage(language);
        ValidateTranslations(translations);

        return new BookUpdated(
            Id,
            title,
            isbn,
            language,
            translations,
            publicationDate,
            publisherId,
            authorIds,
            categoryIds);
    }

    // Validation helper methods
    static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required", nameof(title));
        }

        if (title.Length > 500)
        {
            throw new ArgumentException("Title cannot exceed 500 characters", nameof(title));
        }
    }

    static void ValidateIsbn(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            return; // ISBN is optional
        }

        // Remove hyphens and spaces for validation
        var cleanIsbn = new string([.. isbn.Where(char.IsDigit)]);

        // ISBN-10 or ISBN-13
        if (cleanIsbn.Length is not 10 and not 13)
        {
            throw new ArgumentException("ISBN must be 10 or 13 digits", nameof(isbn));
        }
    }

    static void ValidateLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language is required", nameof(language));
        }

        if (!CultureValidator.IsValidCultureCode(language))
        {
            throw new ArgumentException(
                $"Invalid language code: '{language}'. Must be a valid ISO 639-1 (e.g., 'en'), ISO 639-3 (e.g., 'fil'), or culture code (e.g., 'en-US')",
                nameof(language));
        }
    }

    static void ValidateTranslations(Dictionary<string, BookTranslation>? translations)
    {
        if (translations == null || translations.Count == 0)
        {
            return; // Translations are optional
        }

        // Validate language codes
        if (!CultureValidator.ValidateTranslations(translations, out var invalidCodes))
        {
            throw new ArgumentException(
                $"Invalid language codes in descriptions: {string.Join(", ", invalidCodes)}",
                nameof(translations));
        }

        // Validate description length for each translation
        foreach (var (languageCode, translation) in translations)
        {
            if (translation.Description.Length > 5000)
            {
                throw new ArgumentException(
                    $"Description for language '{languageCode}' cannot exceed 5000 characters",
                    nameof(translations));
            }
        }
    }

    public BookSoftDeleted SoftDelete()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Book is already deleted");
        }

        return new BookSoftDeleted(Id);
    }

    public BookRestored Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Book is not deleted");
        }

        return new BookRestored(Id);
    }

    public BookCoverUpdated UpdateCoverImage(string coverImageUrl)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update cover for a deleted book");
        }

        if (string.IsNullOrWhiteSpace(coverImageUrl))
        {
            throw new ArgumentException("Cover image URL is required", nameof(coverImageUrl));
        }

        return new BookCoverUpdated(Id, coverImageUrl);
    }
}
