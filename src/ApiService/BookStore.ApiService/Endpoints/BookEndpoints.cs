using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookStore.ApiService.Endpoints;

public static class BookEndpoints
{
    public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", SearchBooks)
            .WithName("GetBooks")
            .WithSummary("Get all books")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(2)));

        _ = group.MapGet("/{id:guid}", GetBook)
            .WithName("GetBook")
            .WithSummary("Get book by ID")
            .CacheOutput(policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("If-None-Match"));

        return group;
    }

    static async Task<Ok<PagedListDto<Models.BookDto>>> SearchBooks(
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [AsParameters] PagedRequest request,
        [FromQuery] string? search = null)
    {
        var paging = request.Normalize(paginationOptions.Value);

        // Get current culture set by RequestLocalizationMiddleware
        var language = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        // Dictionaries to hold included documents
        var publishers = new Dictionary<Guid, PublisherProjection>();
        var authors = new Dictionary<Guid, AuthorProjection>();
        var categories = new Dictionary<Guid, CategoryProjection>();

        IPagedList<BookSearchProjection> pagedList;

        if (string.IsNullOrWhiteSpace(search))
        {
            // Return all books if no search query - use Marten's native pagination with Include()
#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
            pagedList = await session.Query<BookSearchProjection>()
                .Include(publishers).On(x => x.PublisherId)!
                .Include(authors).On(x => x.AuthorIds)!
                .Include(categories).On(x => x.CategoryIds)!
                .OrderBy(b => b.Title)
                .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);
#pragma warning restore CS8603
        }
        else
        {
            // Use NGram search for fuzzy, accent-insensitive matching
            var searchQuery = search.Trim();

#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
            pagedList = await session.Query<BookSearchProjection>()
                .Include(publishers).On(x => x.PublisherId)!
                .Include(authors).On(x => x.AuthorIds)!
                .Include(categories).On(x => x.CategoryIds)!
                .Where(b =>
                    b.Title.NgramSearch(searchQuery) ||
                    b.SearchText.NgramSearch(searchQuery) ||
                    (b.Isbn != null && b.Isbn.Contains(searchQuery)))
                .OrderBy(b => b.Title)
                .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value);
#pragma warning restore CS8603
        }

        // Map to DTOs with localized categories, descriptions, and biographies
        var bookDtos = pagedList.Select(book => new Models.BookDto(
            book.Id,
            book.Title,
            book.Isbn,
            book.Language,
            LocalizeDescription(book, language),
            book.PublicationDate,
            Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
            book.PublisherId.HasValue && publishers.TryGetValue(book.PublisherId.Value, out var pub)
                ? new Models.PublisherDto(pub.Id, pub.Name)
                : null,
            [.. book.AuthorIds
                .Select(id => authors.TryGetValue(id, out var author)
                    ? new Models.AuthorDto(author.Id, author.Name, LocalizeBiography(author, language))
                    : null)
                .Where(a => a != null)
                .Cast<Models.AuthorDto>()],
            [.. book.CategoryIds
                .Select(id => categories.TryGetValue(id, out var cat)
                    ? LocalizeCategory(cat, language)
                    : null)
                .Where(c => c != null)
                .Cast<Models.CategoryDto>()]
        )).ToList();

        return TypedResults.Ok(new PagedListDto<Models.BookDto>(
            bookDtos,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount));
    }

    static async Task<IResult> GetBook(
        Guid id,
        [FromServices] IQuerySession session,
        HttpContext context)
    {
        // Get current culture set by RequestLocalizationMiddleware
        var language = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        // Dictionaries to hold included documents
        var publishers = new Dictionary<Guid, PublisherProjection>();
        var authors = new Dictionary<Guid, AuthorProjection>();
        var categories = new Dictionary<Guid, CategoryProjection>();

        // Load book with Include() for related entities
#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
        var book = await session.Query<BookSearchProjection>()
            .Include(publishers).On(x => x.PublisherId)!
            .Include(authors).On(x => x.AuthorIds)!
            .Include(categories).On(x => x.CategoryIds)!
            .Where(b => b.Id == id)
            .SingleOrDefaultAsync();
#pragma warning restore CS8603

        if (book == null)
        {
            return TypedResults.NotFound();
        }

        // Get stream state for ETag
        var streamState = await session.Events.FetchStreamStateAsync(id);
        if (streamState != null)
        {
            var etag = Infrastructure.ETagHelper.GenerateETag(streamState.Version);

            // Check If-None-Match for caching
            if (Infrastructure.ETagHelper.CheckIfNoneMatch(context, etag))
            {
                return Infrastructure.ETagHelper.NotModified(etag);
            }

            Infrastructure.ETagHelper.AddETagHeader(context, etag);
        }

        // Map to DTO with localized categories, description, and biographies
        var bookDto = new Models.BookDto(
            book.Id,
            book.Title,
            book.Isbn,
            book.Language,
            LocalizeDescription(book, language),
            book.PublicationDate,
            Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
            book.PublisherId.HasValue && publishers.TryGetValue(book.PublisherId.Value, out var pub)
                ? new Models.PublisherDto(pub.Id, pub.Name)
                : null,
            [.. book.AuthorIds
                .Select(id => authors.TryGetValue(id, out var author)
                    ? new Models.AuthorDto(author.Id, author.Name, LocalizeBiography(author, language))
                    : null)
                .Where(a => a != null)
                .Cast<Models.AuthorDto>()],
            [.. book.CategoryIds
                .Select(catId => categories.TryGetValue(catId, out var cat)
                    ? LocalizeCategory(cat, language)
                    : null)
                .Where(c => c != null)
                .Cast<Models.CategoryDto>()]);

        return TypedResults.Ok(bookDto);
    }

    // Helper method for category localization with fallback strategy
    static Models.CategoryDto LocalizeCategory(CategoryProjection category, string language)
    {
        // Try full culture code first (e.g., "pt-PT")
        if (category.Translations.TryGetValue(language, out var localized))
        {
            return new Models.CategoryDto(category.Id, localized.Name);
        }

        // Fallback to two-letter ISO language code (e.g., "pt" from "pt-PT")
        try
        {
            var culture = new System.Globalization.CultureInfo(language);
            var twoLetterCode = culture.TwoLetterISOLanguageName;

            if (category.Translations.TryGetValue(twoLetterCode, out var twoLetterLocalized))
            {
                return new Models.CategoryDto(category.Id, twoLetterLocalized.Name);
            }
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            // Invalid culture code - fall through to default
        }

        // Fallback to English
        if (category.Translations.TryGetValue("en", out var englishName))
        {
            return new Models.CategoryDto(category.Id, englishName.Name);
        }

        // Last resort: use first available localization
        var firstName = category.Translations.Values.FirstOrDefault();
        return new Models.CategoryDto(category.Id, firstName?.Name ?? "Unknown");
    }

    // Helper method for book description localization with fallback strategy
    static string? LocalizeDescription(BookSearchProjection book, string language)
    {
        if (book.Translations.Count == 0)
        {
            return null;
        }

        // Try exact language match
        if (book.Translations.TryGetValue(language, out var description))
        {
            return description.Description;
        }

        // Fallback to two-letter ISO language code
        try
        {
            var culture = new System.Globalization.CultureInfo(language);
            var twoLetterCode = culture.TwoLetterISOLanguageName;

            if (book.Translations.TryGetValue(twoLetterCode, out var twoLetterDescription))
            {
                return twoLetterDescription.Description;
            }
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            // Invalid culture code - fall through to default
        }

        // Fallback to English
        if (book.Translations.TryGetValue("en", out var englishDescription))
        {
            return englishDescription.Description;
        }

        // Last resort: use first available description
        return book.Translations.Values.FirstOrDefault()?.Description;
    }

    // Helper method for author biography localization with fallback strategy
    static string? LocalizeBiography(AuthorProjection author, string language)
    {
        if (author.Translations.Count == 0)
        {
            return null;
        }

        // Try exact language match
        if (author.Translations.TryGetValue(language, out var biography))
        {
            return biography.Biography;
        }

        // Fallback to two-letter ISO language code
        try
        {
            var culture = new System.Globalization.CultureInfo(language);
            var twoLetterCode = culture.TwoLetterISOLanguageName;

            if (author.Translations.TryGetValue(twoLetterCode, out var twoLetterBiography))
            {
                return twoLetterBiography.Biography;
            }
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            // Invalid culture code - fall through to default
        }

        // Fallback to English
        if (author.Translations.TryGetValue("en", out var englishBiography))
        {
            return englishBiography.Biography;
        }

        // Last resort: use first available biography
        return author.Translations.Values.FirstOrDefault()?.Biography;
    }
}
