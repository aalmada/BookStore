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

    public async Task ToggleFavoriteAsync(BookDto book, Action<bool> setOptimistic, Action<bool> setRollback)
    {
        var originalState = book.IsFavorite;

        // 1. Optimistic Update
        setOptimistic(!originalState);

        try
        {
            if (originalState)
            {
                await _booksClient.RemoveBookFromFavoritesAsync(book.Id);
                _ = _snackbar.Add("Removed from favorites", Severity.Success);
            }
            else
            {
                await _booksClient.AddBookToFavoritesAsync(book.Id);
                _ = _snackbar.Add("Added to favorites", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Log.FavoriteToggleFailed(_logger, book.Id, ex);
            // 2. Rollback
            setRollback(originalState);
            _ = _snackbar.Add($"Failed to update favorites: {ex.Message}", Severity.Error);
        }
    }

    public async Task RateBookAsync(BookDto book, int rating, Action<int> setOptimistic, Action<int> setRollback)
    {
        var previousRating = book.UserRating;

        // 1. Optimistic update
        setOptimistic(rating);

        try
        {
            await _booksClient.RateBookAsync(book.Id, new BookStore.Client.RateBookRequest(rating));
            _ = _snackbar.Add($"Rated {rating} stars", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.RatingFailed(_logger, book.Id, ex);
            // 2. Rollback
            setRollback(previousRating);
            _ = _snackbar.Add($"Failed to rate: {ex.Message}", Severity.Error);
        }
    }

    public async Task RemoveRatingAsync(BookDto book, Action setOptimistic, Action<int> setRollback)
    {
        if (book.UserRating == 0)
        {
            return;
        }

        var previousRating = book.UserRating;

        // 1. Optimistic update
        setOptimistic();

        try
        {
            await _booksClient.RemoveBookRatingAsync(book.Id);
            _ = _snackbar.Add("Rating removed", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.RatingRemovalFailed(_logger, book.Id, ex);
            // 2. Rollback
            setRollback(previousRating);
            _ = _snackbar.Add($"Failed to remove rating: {ex.Message}", Severity.Error);
        }
    }
}
