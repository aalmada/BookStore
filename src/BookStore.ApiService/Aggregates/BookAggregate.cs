using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Marten.Metadata;

namespace BookStore.ApiService.Aggregates;

public class BookAggregate : ISoftDeleted
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
#pragma warning disable BS3005 // Aggregate properties must have private setters (Marten ISoftDeleted requirement)
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
#pragma warning restore BS3005
    public Dictionary<string, decimal> Prices { get; private set; } = [];
    public List<BookSale> Sales { get; private set; } = [];
    public decimal CurrentDiscountPercentage { get; private set; }
    public CoverImageFormat CoverFormat { get; private set; } = CoverImageFormat.None;
    public long Version { get; private set; }

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
        Deleted = false;
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

    void Apply(BookSoftDeleted _)
    {
        Deleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    void Apply(BookRestored _)
    {
        Deleted = false;
        DeletedAt = null;
    }

    void Apply(BookCoverUpdated @event) => CoverFormat = @event.CoverFormat;

    void Apply(BookSaleScheduled @event)
    {
        // Remove any existing sale with the same start time
        _ = Sales.RemoveAll(s => s.Start == @event.Sale.Start);
        Sales.Add(@event.Sale);
    }

    void Apply(BookSaleCancelled @event) => _ = Sales.RemoveAll(s => s.Start == @event.SaleStart);

    void Apply(BookDiscountUpdated @event) => CurrentDiscountPercentage = @event.DiscountPercentage;

    // Command methods
    public static Result<BookAdded> CreateEvent(
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
        // Validation with Result pattern
        if (id == Guid.Empty)
        {
            return Result.Failure<BookAdded>(Error.Validation(ErrorCodes.Books.IdRequired, "Book ID is required and cannot be empty"));
        }

        var titleResult = ValidateTitle(title);
        if (titleResult.IsFailure)
        {
            return Result.Failure<BookAdded>(titleResult.Error);
        }

        var isbnResult = ValidateIsbn(isbn);
        if (isbnResult.IsFailure)
        {
            return Result.Failure<BookAdded>(isbnResult.Error);
        }

        var languageResult = ValidateLanguage(language);
        if (languageResult.IsFailure)
        {
            return Result.Failure<BookAdded>(languageResult.Error);
        }

        var translationsResult = ValidateTranslations(translations);
        if (translationsResult.IsFailure)
        {
            return Result.Failure<BookAdded>(translationsResult.Error);
        }

        var pricesResult = ValidatePrices(prices);
        if (pricesResult.IsFailure)
        {
            return Result.Failure<BookAdded>(pricesResult.Error);
        }

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

    public Result<BookUpdated> UpdateEvent(
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
        if (Deleted)
        {
            return Result.Failure<BookUpdated>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Cannot update a deleted book"));
        }

        // Validation with Result pattern
        var titleResult = ValidateTitle(title);
        if (titleResult.IsFailure)
        {
            return Result.Failure<BookUpdated>(titleResult.Error);
        }

        var isbnResult = ValidateIsbn(isbn);
        if (isbnResult.IsFailure)
        {
            return Result.Failure<BookUpdated>(isbnResult.Error);
        }

        var languageResult = ValidateLanguage(language);
        if (languageResult.IsFailure)
        {
            return Result.Failure<BookUpdated>(languageResult.Error);
        }

        var translationsResult = ValidateTranslations(translations);
        if (translationsResult.IsFailure)
        {
            return Result.Failure<BookUpdated>(translationsResult.Error);
        }

        var pricesResult = ValidatePrices(prices);
        if (pricesResult.IsFailure)
        {
            return Result.Failure<BookUpdated>(pricesResult.Error);
        }

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
    static Result ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.TitleRequired, "Book title cannot be null or empty"));
        }

        if (title.Length > 500)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.TitleTooLong, "Title cannot exceed 500 characters"));
        }

        return Result.Success();
    }

    static Result ValidateIsbn(string? isbn)
    {
        if (isbn is not null && string.IsNullOrWhiteSpace(isbn))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.IsbnEmpty, "ISBN cannot be empty if provided"));
        }

        if (string.IsNullOrWhiteSpace(isbn))
        {
            return Result.Success(); // ISBN is optional
        }

        // Remove hyphens and spaces for validation
        var cleanIsbn = new string([.. isbn.Where(char.IsDigit)]);

        // ISBN-10 or ISBN-13
        if (cleanIsbn.Length is not 10 and not 13)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.IsbnInvalidFormat, "ISBN must be 10 or 13 digits"));
        }

        return Result.Success();
    }

    static Result ValidateLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.LanguageRequired, "Language cannot be null or empty"));
        }

        if (!CultureValidator.IsValidCultureCode(language))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.LanguageInvalid, $"Invalid language code: {language}"));
        }

        return Result.Success();
    }

    // Validation constants
    public const int MaxDescriptionLength = 5000;

    static Result ValidateTranslations(Dictionary<string, BookTranslation> translations)
    {
        if (translations is null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.TranslationsRequired, "Translations cannot be null"));
        }

        if (translations.Count == 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.TranslationsRequired, "At least one description translation is required"));
        }

        // Validate language codes
        if (!CultureValidator.ValidateTranslations(translations, out var invalidCodes))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.TranslationLanguageInvalid, $"Invalid language codes in descriptions: {string.Join(", ", invalidCodes)}"));
        }

        // Validate translation values and description content
        foreach (var (languageCode, translation) in translations)
        {
            if (translation is null)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.TranslationValueRequired, $"Translation value for language '{languageCode}' cannot be null"));
            }

            if (string.IsNullOrWhiteSpace(translation.Description))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.DescriptionRequired, $"Description for language '{languageCode}' cannot be null or empty"));
            }

            if (translation.Description.Length > MaxDescriptionLength)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.DescriptionTooLong, $"Description for language '{languageCode}' cannot exceed {MaxDescriptionLength} characters"));
            }
        }

        return Result.Success();
    }

    static Result ValidatePrices(Dictionary<string, decimal> prices)
    {
        if (prices is null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.PricesRequired, "Prices cannot be null"));
        }

        if (prices.Count == 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.PricesRequired, "At least one price is required"));
        }

        foreach (var (currencyCode, price) in prices)
        {
            if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.PriceCurrencyInvalid, $"Invalid currency code: '{currencyCode}'"));
            }

            if (price < 0)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.PriceNegative, $"Price for currency '{currencyCode}' cannot be negative"));
            }
        }

        return Result.Success();
    }

    public Result<BookSoftDeleted> SoftDeleteEvent()
    {
        if (Deleted)
        {
            return Result.Failure<BookSoftDeleted>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Book is already deleted"));
        }

        return new BookSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public Result<BookRestored> RestoreEvent()
    {
        if (!Deleted)
        {
            return Result.Failure<BookRestored>(Error.Conflict(ErrorCodes.Books.NotDeleted, "Book is not deleted"));
        }

        return new BookRestored(Id, DateTimeOffset.UtcNow);
    }

    public Result<BookCoverUpdated> UpdateCoverImage(CoverImageFormat format)
    {
        if (Deleted)
        {
            return Result.Failure<BookCoverUpdated>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Cannot update cover for a deleted book"));
        }

        if (format == CoverImageFormat.None)
        {
            return Result.Failure<BookCoverUpdated>(Error.Validation(ErrorCodes.Books.CoverFormatNone, "Cover format cannot be None"));
        }

        return new BookCoverUpdated(Id, format);
    }

    public Result<BookSaleScheduled> ScheduleSale(decimal percentage, DateTimeOffset start, DateTimeOffset end)
    {
        if (Deleted)
        {
            return Result.Failure<BookSaleScheduled>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Cannot schedule sale for a deleted book"));
        }

        if (percentage is <= 0 or >= 100)
        {
            return Result.Failure<BookSaleScheduled>(Error.Validation(ErrorCodes.Books.PriceNegative, "Sale percentage must be greater than 0 and less than 100"));
        }

        if (start >= end)
        {
            return Result.Failure<BookSaleScheduled>(Error.Validation(ErrorCodes.Books.SaleOverlap, "Sale start time must be before end time"));
        }

        // Check for overlapping sales
        if (Sales.Any(s => (start < s.End && end > s.Start)))
        {
            return Result.Failure<BookSaleScheduled>(Error.Conflict(ErrorCodes.Books.SaleOverlap, "Sale period overlaps with an existing sale"));
        }

        var sale = new BookSale(percentage, start, end);
        return new BookSaleScheduled(Id, sale);
    }

    public Result<BookSaleCancelled> CancelSale(DateTimeOffset saleStart)
    {
        if (Deleted)
        {
            return Result.Failure<BookSaleCancelled>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Cannot cancel sale for a deleted book"));
        }

        var sale = Sales.FirstOrDefault(s => s.Start == saleStart);
        if (sale.Equals(default(BookSale)))
        {
            return Result.Failure<BookSaleCancelled>(Error.NotFound(ErrorCodes.Books.SaleNotFound, "No sale found with the specified start time"));
        }

        return new BookSaleCancelled(Id, saleStart);
    }

    public Result<BookDiscountUpdated> ApplyDiscount(decimal percentage)
    {
        if (Deleted)
        {
            return Result.Failure<BookDiscountUpdated>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Cannot update discount for a deleted book"));
        }

        if (percentage is < 0 or >= 100)
        {
            return Result.Failure<BookDiscountUpdated>(Error.Validation(ErrorCodes.Books.PriceNegative, "Discount percentage must be between 0 and 100"));
        }

        return new BookDiscountUpdated(Id, percentage);
    }

    public Result<BookDiscountUpdated> RemoveDiscount()
    {
        if (Deleted)
        {
            return Result.Failure<BookDiscountUpdated>(Error.Conflict(ErrorCodes.Books.AlreadyDeleted, "Cannot update discount for a deleted book"));
        }

        return new BookDiscountUpdated(Id, 0);
    }
}

