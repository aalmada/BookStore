using System.Diagnostics;
using System.Globalization;
using System.Security.Claims; // Need this for ClaimsPrincipal if not implicit
using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Endpoints.Books;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Marten.Linq.MatchesSql;
using Marten.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Npgsql;
using Wolverine;

public static class BookEndpoints
{
    public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder group)
    {
        // Add cover endpoints mapping
        _ = group.MapBookCoverEndpoints();

        _ = group.MapGet("/", SearchBooks)
            .WithName("GetBooks")
            .WithSummary("Get all books");

        _ = group.MapGet("/favorites", GetFavoriteBooks)
            .WithName("GetFavoriteBooks")
            .WithSummary("Get user's favorite books")
            .RequireAuthorization();

        _ = group.MapGet("/{id:guid}", GetBook)
            .WithName("GetBook")
            .WithSummary("Get book by ID");

        _ = group.MapPost("/{id:guid}/favorites", AddFavorite)
            .WithName("AddFavorite")
            .WithSummary("Add book to favorites")
            .RequireAuthorization();

        _ = group.MapDelete("/{id:guid}/favorites", RemoveFavorite)
            .WithName("RemoveFavorite")
            .WithSummary("Remove book from favorites")
            .RequireAuthorization();

        _ = group.MapPost("/{id:guid}/rating", RateBook)
            .WithName("RateBook")
            .WithSummary("Rate a book (1-5 stars)")
            .RequireAuthorization();

        _ = group.MapDelete("/{id:guid}/rating", RemoveRating)
            .WithName("RemoveRating")
            .WithSummary("Remove book rating")
            .RequireAuthorization();

        _ = group.MapPost("/{id:guid}/sales", ScheduleSale)
            .WithName("ScheduleBookSale")
            .WithSummary("Schedule a sale for a book")
            .RequireAuthorization();

        _ = group.MapDelete("/{id:guid}/sales", CancelSale)
            .WithName("CancelBookSale")
            .WithSummary("Cancel a scheduled sale for a book")
            .RequireAuthorization();

        return group;
    }

    /// <summary>
    /// Generates a cover image URL for a book based on its format.
    /// Returns null if the book has no cover.
    /// </summary>
    static string? GenerateCoverUrl(Guid bookId, CoverImageFormat format, string tenantId, HttpRequest request)
    {
        if (format == CoverImageFormat.None)
        {
            return null;
        }

        var baseUrl = $"{request.Scheme}://{request.Host}";
        return $"{baseUrl}/api/books/{bookId}/cover?tenantId={Uri.EscapeDataString(tenantId)}";
    }
    static BookSale? GetActiveSale(List<BookSale>? sales, DateTimeOffset now)
    {
        if (sales is null || sales.Count == 0)
        {
            return null;
        }

        var sale = sales.FirstOrDefault(s => s.IsActive(now));
        return sale.Percentage > 0 ? sale : null;
    }

    static async Task<Ok<PagedListDto<BookDto>>> SearchBooks(
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        [FromServices] ITenantContext tenantContext,
        [AsParameters] BookSearchRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tags = new TagList { { "tenant_id", tenantContext.TenantId } };
        Instrumentation.BookSearches.Add(1, tags);

        var paging = request.Normalize(paginationOptions.Value);
        var culture = CultureInfo.CurrentUICulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        // ... (rest of setup)

        // Check if user is admin - manual check to be robust against RoleClaimType mismatches
        var isAdmin = context.User.Claims.Any(c =>
            (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") &&
            string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase));

        // Fix binding if Currency is null but present in query (workaround for potential AsParameters binding issue)
        if (string.IsNullOrWhiteSpace(request.Currency) && context.Request.Query.TryGetValue("currency", out var currencyQuery))
        {
            request = request with { Currency = currencyQuery.ToString() };
        }

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        // Create cache key including all search parameters
        var cacheKey = $"books:search={request.Search}:author={request.AuthorId}:category={request.CategoryId}:publisher={request.PublisherId}:onSale={request.OnSale}:minPrice={request.MinPrice}:maxPrice={request.MaxPrice}:currency={request.Currency}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}:admin={isAdmin}:tenant={tenantContext.TenantId}";

        var response = await cache.GetOrCreateLocalizedAsync(
            cacheKey,
            async cancel =>
            {
                // Diagnostic: Check total books in projection
                var totalBooks = await session.Query<BookSearchProjection>().CountAsync(cancel);

                // Session is already scoped and tenant-aware

                // Dictionaries to hold included documents
                var publishers = new Dictionary<Guid, PublisherProjection>();
                var authors = new Dictionary<Guid, AuthorProjection>();
                var categories = new Dictionary<Guid, CategoryProjection>();
                var statistics = new Dictionary<Guid, BookStatistics>();

                // Build query incrementally
#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
                IQueryable<BookSearchProjection> query = session.Query<BookSearchProjection>()
                    .Include(publishers).On(x => x.PublisherId)!
                    .Include(authors).On(x => x.AuthorIds)!
                    .Include(categories).On(x => x.CategoryIds)!;

                if (!isAdmin)
                {
                    query = query.Where(b => !b.Deleted);
                }

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

                // Filter by active sales if requested
                if (request.OnSale == true)
                {
                    var currentTime = DateTimeOffset.UtcNow;
                    query = query.Where(b => b.Sales.Any(s => s.Start <= currentTime && s.End > currentTime));
                }

                // If Currency is specified but NO price range, we must still filter by currency existence
                if (!string.IsNullOrWhiteSpace(request.Currency) && !request.MinPrice.HasValue && !request.MaxPrice.HasValue)
                {
                    query = query.Where(b => b.Prices.Any(p => p.Currency == request.Currency));
                }

                // Filter by price range if specified
                if (request.MinPrice.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(request.Currency))
                    {
                        query = query.Where(b => b.CurrentPrices.Any(p => p.Currency == request.Currency && p.Value >= request.MinPrice.Value));
                    }
                    else
                    {
                        query = query.Where(b => b.CurrentPrices.Any(p => p.Value >= request.MinPrice.Value));
                    }
                }

                if (request.MaxPrice.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(request.Currency))
                    {
                        query = query.Where(b => b.CurrentPrices.Any(p => p.Currency == request.Currency && p.Value <= request.MaxPrice.Value));
                    }
                    else
                    {
                        query = query.Where(b => b.CurrentPrices.Any(p => p.Value <= request.MaxPrice.Value));
                    }
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
                        .OrderByDescending(b => b.PublicationDateString)
                        .ThenBy(b => b.Title),
                    ("date", "asc") => query
                        .OrderBy(b => b.PublicationDateString)
                        .ThenBy(b => b.Title),
                    ("title", "desc") => query
                        .OrderByDescending(b => b.Title),
                    _ => query
                        .OrderBy(b => b.Title) // Default to Title asc
                };

                // Execute query with pagination
                var pagedList = await query
                    .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancel);

                // Load statistics explicitly to avoid potential issues with Includes on missing tables/documents
                var bookIds = pagedList.Select(b => b.Id).ToArray();
                var loadedStats = await session.LoadManyAsync<BookStatistics>(cancel, bookIds);
                foreach (var stat in loadedStats)
                {
                    statistics[stat.Id] = stat;
                }
#pragma warning restore CS8603

                // Map to DTOs with localized descriptions, biographies, and category names
                var now = DateTimeOffset.UtcNow;
                var bookDtos = pagedList.Select(book =>
                {
                    var activeSale = GetActiveSale(book.Sales, now);
                    return new BookDto(
                        book.Id,
                        book.Title,
                        book.Isbn,
                        book.OriginalLanguage,
                        CultureInfo.GetCultureInfo(book.OriginalLanguage).DisplayName,
                        LocalizationHelper.GetLocalizedValue(book.Descriptions, culture, defaultCulture, ""),
                        book.PublicationDate,
                        BookStore.ApiService.Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
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
                            .Cast<CategoryDto>()],
                        false, // IsFavorite is always false in cache
                        statistics.TryGetValue(book.Id, out var stats) ? stats.LikeCount : 0,
                        stats?.AverageRating ?? 0f,
                        stats?.RatingCount ?? 0,
                        0, // UserRating is always 0 in cache, will be overlaid if authenticated
                        book.Prices.ToDictionary(p => p.Currency, p => p.Value),
                        GenerateCoverUrl(book.Id, book.CoverFormat, tenantContext.TenantId, context.Request),
                        activeSale,
                        book.CurrentPrices,
                        book.Deleted
                    );
                }).ToList();

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

        // Overlay user favorites if authenticated
        // This pattern prevents cache explosion by keeping the cache generic and applying user-specific data at runtime
        if (context.User.Identity?.IsAuthenticated == true && response.Items.Count > 0)
        {
            var userId = context.User.GetUserId();
            if (userId != Guid.Empty)
            {
                // Load user profile using injected session
                var profile = await session.LoadAsync<UserProfile>(userId, cancellationToken);
                if (profile != null)
                {
                    var updatedItems = response.Items.Select(b =>
                    {
                        var result = b;
                        if (profile.FavoriteBookIds.Contains(b.Id))
                        {
                            result = result with { IsFavorite = true };
                        }

                        if (profile.BookRatings.TryGetValue(b.Id, out var rating))
                        {
                            result = result with { UserRating = rating };
                        }

                        return result;
                    }).ToList();

                    response = response with { Items = updatedItems };
                }
            }
        }

        stopwatch.Stop();
        Instrumentation.SearchDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

        return TypedResults.Ok(response);
    }

    static async Task<IResult> GetFavoriteBooks(
        [FromServices] IQuerySession session,
        [FromServices] IOptions<PaginationOptions> paginationOptions,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [AsParameters] OrderedPagedRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // Require authentication
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not authenticated.")).ToProblemDetails();
        }

        var paging = request.Normalize(paginationOptions.Value);
        var culture = CultureInfo.CurrentUICulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        // Check if user is admin
        var isAdmin = context.User.Claims.Any(c =>
            (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") &&
            string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase));

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        // Session is injected

        // Load user's profile to get favorite book IDs
        var profile = await session.LoadAsync<UserProfile>(userId, cancellationToken);

        if (profile == null || profile.FavoriteBookIds.Count == 0)
        {
            // Return empty list if user has no favorites
            return TypedResults.Ok(new PagedListDto<BookDto>(
                [],
                paging.Page!.Value,
                paging.PageSize!.Value,
                0));
        }

        // Dictionaries to hold included documents
        var publishers = new Dictionary<Guid, PublisherProjection>();
        var authors = new Dictionary<Guid, AuthorProjection>();
        var categories = new Dictionary<Guid, CategoryProjection>();
        var statistics = new Dictionary<Guid, BookStatistics>();

        // Query books that are in user's favorites
#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
        var query = session.Query<BookSearchProjection>()
            .Include(publishers).On(x => x.PublisherId)!
            .Include(authors).On(x => x.AuthorIds)!
            .Include(categories).On(x => x.CategoryIds)!
            .Where(b => profile.FavoriteBookIds.Contains(b.Id));

        // Respect admin/soft-delete rules
        if (!isAdmin)
        {
            query = query.Where(b => !b.Deleted);
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
                .OrderByDescending(b => b.PublicationDateString)
                .ThenBy(b => b.Title),
            ("date", "asc") => query
                .OrderBy(b => b.PublicationDateString)
                .ThenBy(b => b.Title),
            ("title", "desc") => query
                .OrderByDescending(b => b.Title),
            _ => query
                .OrderBy(b => b.Title) // Default to Title asc
        };

        // Execute query with pagination
        var pagedList = await query
            .ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancellationToken);

        // Load statistics explicitly
        var bookIds = pagedList.Select(b => b.Id).ToArray();
        var loadedStats = await session.LoadManyAsync<BookStatistics>(cancellationToken, bookIds);
        foreach (var stat in loadedStats)
        {
            statistics[stat.Id] = stat;
        }
#pragma warning restore CS8603

        // Map to DTOs with localized descriptions
        var now = DateTimeOffset.UtcNow;
        var bookDtos = pagedList.Select(book =>
        {
            var activeSale = GetActiveSale(book.Sales, now);
            return new BookDto(
                book.Id,
                book.Title,
                book.Isbn,
                book.OriginalLanguage,
                CultureInfo.GetCultureInfo(book.OriginalLanguage).DisplayName,
                LocalizationHelper.GetLocalizedValue(book.Descriptions, culture, defaultCulture, ""),
                book.PublicationDate,
                BookStore.ApiService.Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
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
                    .Cast<CategoryDto>()],
                true, // IsFavorite is always true for favorites endpoint
                statistics.TryGetValue(book.Id, out var stats) ? stats.LikeCount : 0,
                stats?.AverageRating ?? 0f,
                stats?.RatingCount ?? 0,
                profile.BookRatings.TryGetValue(book.Id, out var userRating) ? userRating : 0,
                book.Prices.ToDictionary(p => p.Currency, p => p.Value),
                GenerateCoverUrl(book.Id, book.CoverFormat, session.TenantId, context.Request),
                activeSale,
                book.CurrentPrices,
                book.Deleted
            );
        }).ToList();

        var response = new PagedListDto<BookDto>(
            bookDtos,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount);

        return TypedResults.Ok(response);
    }

    static async Task<IResult> GetBook(
        Guid id,
        [FromServices] IQuerySession session,
        [FromServices] IOptions<LocalizationOptions> localizationOptions,
        [FromServices] HybridCache cache,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        Instrumentation.BookViews.Add(1, new TagList { { "tenant_id", tenantContext.TenantId } });

        var culture = CultureInfo.CurrentUICulture.Name;
        var defaultCulture = localizationOptions.Value.DefaultCulture;

        // Check ETag first (before cache) for conditional requests
        // Session is injected
        var streamState = await session.Events.FetchStreamStateAsync(id, cancellationToken);

        if (streamState != null)
        {
            var etag = BookStore.ApiService.Infrastructure.ETagHelper.GenerateETag(streamState.Version);

            // Check If-None-Match for caching
            if (BookStore.ApiService.Infrastructure.ETagHelper.CheckIfNoneMatch(context, etag))
            {
                return BookStore.ApiService.Infrastructure.ETagHelper.NotModified(etag);
            }

            BookStore.ApiService.Infrastructure.ETagHelper.AddETagHeader(context, etag);
        }

        // Check if user is admin - manual check to be robust against RoleClaimType mismatches
        var isAdmin = context.User.Claims.Any(c =>
            (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") &&
            string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase));

        var response = await cache.GetOrCreateLocalizedAsync(
            $"book:{id}:admin={isAdmin}:tenant={tenantContext.TenantId}",
            async cancel =>
            {
                // Dictionaries to hold included documents
                var publishers = new Dictionary<Guid, PublisherProjection>();
                var authors = new Dictionary<Guid, AuthorProjection>();
                var categories = new Dictionary<Guid, CategoryProjection>();
                var statistics = new Dictionary<Guid, BookStatistics>();

                // Load book with Include() for related entities
                // Since session is tenant-scoped, Includes will automatically load for the same tenant!
#pragma warning disable CS8603 // Possible null reference return
                var bookQuery = session.Query<BookSearchProjection>()
                    .Include(publishers).On(x => x.PublisherId)!
                    .Include(authors).On(x => x.AuthorIds)!
                    .Include(categories).On(x => x.CategoryIds)!
                    //.Include(statistics).On(x => x.Id)!
                    //.Include(statistics).On(x => x.Id)!
                    .Where(b => b.Id == id);

                if (!isAdmin)
                {
                    bookQuery = bookQuery.Where(b => !b.Deleted);
                }

                var book = await bookQuery.SingleOrDefaultAsync(cancel);
#pragma warning restore CS8603

                if (book != null)
                {
                    var bookStats = await session.LoadAsync<BookStatistics>(book.Id, cancel);
                    if (bookStats != null)
                    {
                        statistics[book.Id] = bookStats;
                    }
                }

                if (book == null)
                {
                    return (BookDto?)null;
                }

                // Map to DTO with localized values
                var now = DateTimeOffset.UtcNow;
                var activeSale = GetActiveSale(book.Sales, now);
                return new BookDto(
                    book.Id,
                    book.Title,
                    book.Isbn,
                    book.OriginalLanguage,
                    CultureInfo.GetCultureInfo(book.OriginalLanguage).DisplayName,
                    LocalizationHelper.GetLocalizedValue(book.Descriptions, culture, defaultCulture, ""),
                    book.PublicationDate,
                    BookStore.ApiService.Helpers.BookHelpers.IsPreRelease(book.PublicationDate),
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
                        .Cast<CategoryDto>()],
                    false, // IsFavorite is always false in cache
                    statistics.TryGetValue(book.Id, out var stats) ? stats.LikeCount : 0,
                    stats?.AverageRating ?? 0f,
                    stats?.RatingCount ?? 0,
                    0, // UserRating is always 0 in cache, will be overlaid if authenticated
                    book.Prices.ToDictionary(p => p.Currency, p => p.Value),
                    GenerateCoverUrl(book.Id, book.CoverFormat, tenantContext.TenantId, context.Request),
                    activeSale,
                    book.CurrentPrices,
                    book.Deleted
                    );
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: [CacheTags.ForItem(CacheTags.BookItemPrefix, id)],
            token: cancellationToken);

        if (response is null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Books.BookNotFound, "Book not found.")).ToProblemDetails();
        }

        // Overlay user favorites if authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.GetUserId();
            if (userId != Guid.Empty)
            {
                var profile = await session.LoadAsync<UserProfile>(userId, cancellationToken);
                if (profile != null && profile.FavoriteBookIds.Contains(id))
                {
                    response = response with { IsFavorite = true };
                }
            }
        }

        // Overlay user rating if authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.GetUserId();
            if (userId != Guid.Empty)
            {
                var user = await session.LoadAsync<UserProfile>(userId, cancellationToken);
                if (user != null)
                {
                    if (user.BookRatings.TryGetValue(id, out var rating))
                    {
                        response = response with { UserRating = rating };
                    }
                }
            }
        }

        return TypedResults.Ok(response);
    }

    static async Task<IResult> AddFavorite(
        Guid id,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not authenticated.")).ToProblemDetails();
        }

        await bus.InvokeAsync(new AddBookToFavorites(userId, id), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<IResult> RemoveFavorite(
        Guid id,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not authenticated.")).ToProblemDetails();
        }

        await bus.InvokeAsync(new RemoveBookFromFavorites(userId, id), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<IResult> RateBook(
        Guid id,
        [FromBody] RateBookRequest request,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Rating is < 1 or > 5)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.RatingInvalid, "Rating must be between 1 and 5")).ToProblemDetails();
        }

        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not authenticated.")).ToProblemDetails();
        }

        await bus.InvokeAsync(new RateBook(userId, id, request.Rating), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<IResult> RemoveRating(
        Guid id,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not authenticated.")).ToProblemDetails();
        }

        await bus.InvokeAsync(new RemoveBookRating(userId, id), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<IResult> ScheduleSale(
        Guid id,
        [FromBody] ScheduleSaleRequest request,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var etag = context.Request.Headers.IfMatch.FirstOrDefault();
        var command = new ScheduleSale(id, request.Percentage, request.Start, request.End)
        {
            ETag = etag
        };

        try
        {
            return await bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BookEndpoints] Failed to invoke ScheduleSale: {ex}");
            return Results.Problem(ex.ToString());
        }
    }

    static async Task<IResult> CancelSale(
        Guid id,
        [FromQuery] DateTimeOffset saleStart,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var etag = context.Request.Headers.IfMatch.FirstOrDefault();
        var command = new CancelSale(id, saleStart)
        {
            ETag = etag
        };

        return await bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
    }

    record RateBookRequest(int Rating);
    record ScheduleSaleRequest(decimal Percentage, DateTimeOffset Start, DateTimeOffset End);
}
