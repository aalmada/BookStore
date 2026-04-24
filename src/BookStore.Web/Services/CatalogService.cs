using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Logging;
using MudBlazor;

namespace BookStore.Web.Services;

public class CatalogService
{
    readonly IBooksClient _booksClient;
    readonly ISnackbar _snackbar;
    readonly ILogger<CatalogService> _logger;

    public CatalogService(IBooksClient booksClient, ISnackbar snackbar, ILogger<CatalogService> logger)
    {
        _booksClient = booksClient;
        _snackbar = snackbar;
        _logger = logger;
    }

    public async Task ToggleFavoriteAsync(BookDto book, ReactiveQuery<BookDto?> query,
        CancellationToken cancellationToken = default)
    {
        var originalState = book.IsFavorite;

        try
        {
            await query.MutateAsync(
                current => current == null
                    ? null
                    : current with
                    {
                        IsFavorite = !originalState,
                        LikeCount = originalState ? current.LikeCount - 1 : current.LikeCount + 1
                    },
                async ct =>
                {
                    if (originalState)
                    {
                        await _booksClient.RemoveBookFromFavoritesAsync(book.Id, cancellationToken: ct);
                    }
                    else
                    {
                        await _booksClient.AddBookToFavoritesAsync(book.Id, cancellationToken: ct);
                    }
                },
                cancellationToken);

            if (originalState)
            {
                _ = _snackbar.Add("Removed from favorites", Severity.Success);
            }
            else
            {
                _ = _snackbar.Add("Added to favorites", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Log.FavoriteToggleFailed(_logger, book.Id, ex);
            _ = _snackbar.Add($"Failed to update favorites: {ex.Message}", Severity.Error);
        }
    }

    public async Task ToggleFavoriteAsync(BookDto book, ReactiveQuery<PagedListDto<BookDto>> query,
        CancellationToken cancellationToken = default)
    {
        var originalState = book.IsFavorite;

        try
        {
            await query.MutateAsync(
                currentList =>
                {
                    var items = currentList.Items.ToList();
                    var index = items.FindIndex(b => b.Id == book.Id);
                    if (index != -1)
                    {
                        items[index] = items[index] with
                        {
                            IsFavorite = !originalState,
                            LikeCount = originalState ? items[index].LikeCount - 1 : items[index].LikeCount + 1
                        };
                    }

                    return new PagedListDto<BookDto>(
                        items,
                        currentList.PageNumber,
                        currentList.PageSize,
                        currentList.TotalItemCount
                    );
                },
                async ct =>
                {
                    if (originalState)
                    {
                        await _booksClient.RemoveBookFromFavoritesAsync(book.Id, cancellationToken: ct);
                    }
                    else
                    {
                        await _booksClient.AddBookToFavoritesAsync(book.Id, cancellationToken: ct);
                    }
                },
                cancellationToken);

            if (originalState)
            {
                _ = _snackbar.Add("Removed from favorites", Severity.Success);
            }
            else
            {
                _ = _snackbar.Add("Added to favorites", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Log.FavoriteToggleFailed(_logger, book.Id, ex);
            _ = _snackbar.Add($"Failed to update favorites: {ex.Message}", Severity.Error);
        }
    }

    public async Task RateBookAsync(BookDto book, int rating, ReactiveQuery<BookDto?> query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await query.MutateAsync(
                current => current == null ? null : current with { UserRating = rating },
                ct => _booksClient.RateBookAsync(book.Id, new BookStore.Client.RateBookRequest(rating), cancellationToken: ct),
                cancellationToken);

            _ = _snackbar.Add($"Rated {rating} stars", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.RatingFailed(_logger, book.Id, ex);
            _ = _snackbar.Add($"Failed to rate: {ex.Message}", Severity.Error);
        }
    }

    public async Task RemoveRatingAsync(BookDto book, ReactiveQuery<BookDto?> query,
        CancellationToken cancellationToken = default)
    {
        if (book.UserRating == 0)
        {
            return;
        }

        try
        {
            await query.MutateAsync(
                current => current == null ? null : current with { UserRating = 0 },
                ct => _booksClient.RemoveBookRatingAsync(book.Id, cancellationToken: ct),
                cancellationToken);

            _ = _snackbar.Add("Rating removed", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.RatingRemovalFailed(_logger, book.Id, ex);
            _ = _snackbar.Add($"Failed to remove rating: {ex.Message}", Severity.Error);
        }
    }

    public async Task SoftDeleteBookAsync(BookDto book, ReactiveQuery<BookDto?> query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await query.MutateAsync(
                current => current == null ? null : current with { IsDeleted = true },
                ct => _booksClient.SoftDeleteBookAsync(book.Id, book.ETag, ct),
                cancellationToken);

            _ = _snackbar.Add("Book deleted", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.BookDeleteFailed(_logger, book.Id, ex);
            _ = _snackbar.Add($"Failed to delete book: {ex.Message}", Severity.Error);
        }
    }

    public async Task RestoreBookAsync(BookDto book, ReactiveQuery<BookDto?> query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await query.MutateAsync(
                current => current == null ? null : current with { IsDeleted = false },
                ct => _booksClient.RestoreBookAsync(book.Id, etag: book.ETag, cancellationToken: ct),
                cancellationToken);

            _ = _snackbar.Add("Book restored", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.BookRestoreFailed(_logger, book.Id, ex);
            _ = _snackbar.Add($"Failed to restore book: {ex.Message}", Severity.Error);
        }
    }
}
