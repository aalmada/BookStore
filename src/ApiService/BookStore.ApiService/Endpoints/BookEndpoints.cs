using System.Globalization;
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookStore.ApiService.Endpoints;

public static class BookEndpoints
{
    public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", SearchBooks)
            .WithName("GetBooks")
            .WithSummary("Get all books");

        _ = group.MapGet("/{id:guid}", GetBook)
            .WithName("GetBook")
            .WithSummary("Get book by ID");

        return group;
    }

    static async Task<Ok<PagedListDto<BookDto>>> SearchBooks(
        [FromServices] IDocumentStore store,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        [AsParameters] BookSearchRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var paging = request.Normalize(paginationOptions.Value);
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        // Create cache key including all search parameters
        var cacheKey = $"books:search={request.Search}:author={request.AuthorId}:category={request.CategoryId}:publisher={request.PublisherId}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

        var response = await cache.GetOrCreateLocalizedAsync(
            cacheKey,
            async cancel =>
            {
                await using var session = store.QuerySession();

                // Dictionaries to hold included documents
                var publishers = new Dictionary<Guid, PublisherProjection>();
                var authors = new Dictionary<Guid, AuthorProjection>();
                var categories = new Dictionary<Guid, CategoryProjection>();

                // Build query incrementally
#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
                IQueryable<BookSearchProjection> query = session.Query<BookSearchProjection>()
                    .Include(publishers).On(x => x.PublisherId)!
                    .Include(authors).On(x => x.AuthorIds)!
                    .Include(categories).On(x => x.CategoryIds)!;

                // Add search filter if search term is provided
                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var searchQuery = request.Search.Trim();
                    query = query.Where(b =>
                        b.SearchText.NgramSearch(searchQuery) ||
                        (b.Isbn != null && b.Isbn.Contains(searchQuery)));
                }

                if (request.AuthorId.HasValue)
                {
                    query = query.Where(b => b.AuthorIds.Contains(request.AuthorId.Value));
                }

                if (request.CategoryId.HasValue)
                {
                    query = query.Where(b => b.CategoryIds.Contains(request.CategoryId.Value));
                }

                if (request.PublisherId.HasValue)
                {
                    query = query.Where(b => b.PublisherId == request.PublisherId.Value);
                }

                // Apply sorting
                query = (normalizedSortBy, normalizedSortOrder) switch
                {
                    ("publisher", "desc") => query
                        .OrderByDescending(b => b.PublisherName)
                        .ThenBy(b => b.Title),
                    ("publisher", "asc") => query
                        .OrderBy(b => b.PublisherName)
                        .ThenBy(b => b.Title),
                    ("date", "desc") => query
                        .OrderByDescending(b => b.PublicationDate.GetValueOrDefault().Year)
                        .ThenByDescending(b => b.PublicationDate.GetValueOrDefault().Month)
                        .ThenByDescending(b => b.PublicationDate.GetValueOrDefault().Day)
                        .ThenBy(b => b.Title),
                    ("date", "asc") => query
                        .OrderBy(b => b.PublicationDate.GetValueOrDefault().Year)
                        .ThenBy(b => b.PublicationDate.GetValueOrDefault().Month)
                        .ThenBy(b => b.PublicationDate.GetValueOrDefault().Day)
                        .ThenBy(b => b.Title),
                    ("title", "desc") => query
                        .OrderByDescending(b => b.Title),
                    _ => query
                        .OrderBy(b => b.Title) // Default to Title asc
                };

                // Execute query with pagination
                var pagedList = await query
                    .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancel);
#pragma warning restore CS8603

                // Map to DTOs with localized descriptions, biographies, and category names
                var bookDtos = pagedList.Select(book => new BookDto(
                    book.Id,
                    book.Title,
                    book.Isbn,
                    book.OriginalLanguage,
                    CultureInfo.GetCultureInfo(book.OriginalLanguage).DisplayName,
                    LocalizationHelper.GetLocalizedValue(book.Descriptions, culture, defaultCulture, ""),
                    book.PublicationDate,
                    Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
                    book.PublisherId.HasValue && publishers.TryGetValue(book.PublisherId.Value, out var pub)
                        ? new PublisherDto(pub.Id, pub.Name)
                        : null,
                    [.. book.AuthorIds
                        .Select(id => authors.TryGetValue(id, out var author)
                            ? new AuthorDto(
                                author.Id,
                                author.Name,
                                LocalizationHelper.GetLocalizedValue(author.Biographies, culture, defaultCulture, ""))
                            : null)
                        .Where(a => a != null)
                        .Cast<AuthorDto>()],
                    [.. book.CategoryIds
                        .Select(id => categories.TryGetValue(id, out var cat)
                            ? new CategoryDto(
                                cat.Id,
                                LocalizationHelper.GetLocalizedValue(cat.Names, culture, defaultCulture, "Unknown"))
                            : null)
                        .Where(c => c != null)
                        .Cast<CategoryDto>()]
                )).ToList();

                return new PagedListDto<BookDto>(
                    bookDtos,
                    pagedList.PageNumber,
                    pagedList.PageSize,
                    pagedList.TotalItemCount);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(2),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            },
            tags: [CacheTags.BookList],
            token: cancellationToken);

        return TypedResults.Ok(response);
    }

    static async Task<Results<Ok<BookDto>, NotFound, StatusCodeHttpResult>> GetBook(
        Guid id,
        [FromServices] IDocumentStore store,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var culture = CultureInfo.CurrentCulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        // Check ETag first (before cache) for conditional requests
        await using var session = store.QuerySession();
        var streamState = await session.Events.FetchStreamStateAsync(id, cancellationToken);

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

        var response = await cache.GetOrCreateLocalizedAsync(
            $"book:{id}",
            async cancel =>
            {
                // Dictionaries to hold included documents
                var publishers = new Dictionary<Guid, PublisherProjection>();
                var authors = new Dictionary<Guid, AuthorProjection>();
                var categories = new Dictionary<Guid, CategoryProjection>();

                // Load book with Include() for related entities
                // Since session is tenant-scoped, Includes will automatically load for the same tenant!
#pragma warning disable CS8603 // Possible null reference return
                var book = await session.Query<BookSearchProjection>()
                    .Include(publishers).On(x => x.PublisherId)!
                    .Include(authors).On(x => x.AuthorIds)!
                    .Include(categories).On(x => x.CategoryIds)!
                    .Where(b => b.Id == id)
                    .SingleOrDefaultAsync(cancel);
#pragma warning restore CS8603

                if (book == null)
                {
                    return (BookDto?)null;
                }

                // Map to DTO with localized values
                return new BookDto(
                    book.Id,
                    book.Title,
                    book.Isbn,
                    book.OriginalLanguage,
                    CultureInfo.GetCultureInfo(book.OriginalLanguage).DisplayName,
                    LocalizationHelper.GetLocalizedValue(book.Descriptions, culture, defaultCulture, ""),
                    book.PublicationDate,
                    Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
                    book.PublisherId.HasValue && publishers.TryGetValue(book.PublisherId.Value, out var pub)
                        ? new PublisherDto(pub.Id, pub.Name)
                        : null,
                    [.. book.AuthorIds
                        .Select(authorId => authors.TryGetValue(authorId, out var author)
                            ? new AuthorDto(
                                author.Id,
                                author.Name,
                                LocalizationHelper.GetLocalizedValue(author.Biographies, culture, defaultCulture, ""))
                            : null)
                        .Where(a => a != null)
                        .Cast<AuthorDto>()],
                    [.. book.CategoryIds
                        .Select(catId => categories.TryGetValue(catId, out var cat)
                            ? new CategoryDto(
                                catId,
                                LocalizationHelper.GetLocalizedValue(cat.Names, culture, defaultCulture, "Unknown"))
                            : null)
                        .Where(c => c != null)
                        .Cast<CategoryDto>()]);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheTags.ForItem(CacheTags.BookItemPrefix, id)],
            token: cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }
}
