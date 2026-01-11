using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
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
    public Dictionary<string, decimal> Prices { get; private set; } = [];
    public string? CoverImageUrl { get; private set; }

    // Marten uses this for rehydration
    void Apply(BookAdded @event)
    {
        Id = @event.Id;
        Title = @event.Title;
        Isbn = @event.Isbn;
        Language = @event.Language;
        Translations = @event.Translations;
        PublicationDate = @event.PublicationDate;
        PublisherId = @event.PublisherId;
        AuthorIds = @event.AuthorIds;
        CategoryIds = @event.CategoryIds;
        Prices = @event.Prices;
        IsDeleted = false;
    }

    void Apply(BookUpdated @event)
    {
        Title = @event.Title;
        Isbn = @event.Isbn;
        Language = @event.Language;
        Translations = @event.Translations;
        PublicationDate = @event.PublicationDate;
        PublisherId = @event.PublisherId;
        AuthorIds = @event.AuthorIds;
        CategoryIds = @event.CategoryIds;
        Prices = @event.Prices;
    }

    void Apply(BookSoftDeleted _) => IsDeleted = true;

    void Apply(BookRestored _) => IsDeleted = false;

    void Apply(BookCoverUpdated @event) => CoverImageUrl = @event.CoverImageUrl;

    // Command methods
    public static BookAdded CreateEvent(
        Guid id,
        string title,
        string? isbn,
        string language,
        Dictionary<string, BookTranslation> translations,
        PartialDate? publicationDate,
        Guid? publisherId,
        List<Guid> authorIds,
        List<Guid> categoryIds,
        Dictionary<string, decimal> prices)
    {
        // Validate all inputs before creating event
        ValidateTitle(title);
        ValidateIsbn(isbn);
        ValidateLanguage(language);
        ValidateTranslations(translations);
        ValidatePrices(prices);

        return new BookAdded(
            id,
            title,
            isbn,
            language,
            translations,
            publicationDate,
            publisherId,
            authorIds,
            categoryIds,
            prices);
    }

    public BookUpdated UpdateEvent(
        string title,
        string? isbn,
        string language,
        Dictionary<string, BookTranslation> translations,
        PartialDate? publicationDate,
        Guid? publisherId,
        List<Guid> authorIds,
        List<Guid> categoryIds,
        Dictionary<string, decimal> prices)
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
        ValidatePrices(prices);

        return new BookUpdated(
            Id,
            title,
            isbn,
            language,
            translations,
            publicationDate,
            publisherId,
            authorIds,
            categoryIds,
            prices);
    }

    // Validation helper methods
    static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Book title cannot be null or empty", nameof(title));
        }

        if (title.Length > 500)
        {
            throw new ArgumentException("Title cannot exceed 500 characters", nameof(title));
        }
    }

    static void ValidateIsbn(string? isbn)
    {
        if (isbn is not null && string.IsNullOrWhiteSpace(isbn))
        {
            throw new ArgumentException("ISBN cannot be empty if provided", nameof(isbn));
        }

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
            throw new ArgumentException("Language cannot be null or empty", nameof(language));
        }

        if (!CultureValidator.IsValidCultureCode(language))
        {
            throw new ArgumentException(
                $"Invalid language code: {language}",
                nameof(language));
        }
    }

    // Validation constants
    public const int MaxDescriptionLength = 5000;

    static void ValidateTranslations(Dictionary<string, BookTranslation> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        if (translations.Count == 0)
        {
            throw new ArgumentException("At least one description translation is required", nameof(translations));
        }

        // NOTE: We do NOT validate for a specific default language here because:
        // 1. Configuration can change over time (e.g., default language changes from "en" to "pt")
        // 2. During projection rebuilds, old events must remain valid
        // 3. The handler layer validates default language presence before creating events

        // Validate language codes
        if (!CultureValidator.ValidateTranslations(translations, out var invalidCodes))
        {
            throw new ArgumentException(
                $"Invalid language codes in descriptions: {string.Join(", ", invalidCodes)}",
                nameof(translations));
        }

        // Validate translation values and description content
        foreach (var (languageCode, translation) in translations)
        {
            if (translation is null)
            {
                throw new ArgumentException($"Translation value for language '{languageCode}' cannot be null", nameof(translations));
            }

            if (string.IsNullOrWhiteSpace(translation.Description))
            {
                throw new ArgumentException($"Description for language '{languageCode}' cannot be null or empty", nameof(translations));
            }

            if (translation.Description.Length > MaxDescriptionLength)
            {
                throw new ArgumentException(
                    $"Description for language '{languageCode}' cannot exceed {MaxDescriptionLength} characters",
                    nameof(translations));
            }
        }
    }

    static void ValidatePrices(Dictionary<string, decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (prices.Count == 0)
        {
            throw new ArgumentException("At least one price is required", nameof(prices));
        }

        foreach (var (currencyCode, price) in prices)
        {
            if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            {
                throw new ArgumentException($"Invalid currency code: '{currencyCode}'", nameof(prices));
            }

            if (price < 0)
            {
                throw new ArgumentException($"Price for currency '{currencyCode}' cannot be negative", nameof(prices));
            }
        }
    }

    public BookSoftDeleted SoftDeleteEvent()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Book is already deleted");
        }

        return new BookSoftDeleted(Id);
    }

    public BookRestored RestoreEvent()
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
