using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Logging;

namespace BookStore.Web.Services;

public class CatalogService
{
    readonly IBooksClient _booksClient;
    readonly INotificationService _notificationService;
    readonly ILogger<CatalogService> _logger;

    public CatalogService(IBooksClient booksClient, INotificationService notificationService, ILogger<CatalogService> logger)
    {
        _booksClient = booksClient;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task ToggleFavoriteAsync(BookDto book, Action<bool> setOptimistic, Action<bool> setRollback,
        CancellationToken cancellationToken = default)
    {
        var originalState = book.IsFavorite;

        // 1. Optimistic Update
        setOptimistic(!originalState);

        try
        {
            if (originalState)
            {
                await _booksClient.RemoveBookFromFavoritesAsync(book.Id, cancellationToken: cancellationToken);
                _notificationService.Add("Removed from favorites", NotificationSeverity.Success);
            }
            else
            {
                await _booksClient.AddBookToFavoritesAsync(book.Id, cancellationToken: cancellationToken);
                _notificationService.Add("Added to favorites", NotificationSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            Log.FavoriteToggleFailed(_logger, book.Id, ex);
            // 2. Rollback
            setRollback(originalState);
            _notificationService.Add($"Failed to update favorites: {ex.Message}", NotificationSeverity.Error);
        }
    }

    public async Task RateBookAsync(BookDto book, int rating, Action<int> setOptimistic, Action<int> setRollback,
        CancellationToken cancellationToken = default)
    {
        var previousRating = book.UserRating;

        // 1. Optimistic update
        setOptimistic(rating);

        try
        {
            await _booksClient.RateBookAsync(book.Id, new BookStore.Client.RateBookRequest(rating),
                cancellationToken: cancellationToken);
            _notificationService.Add($"Rated {rating} stars", NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            Log.RatingFailed(_logger, book.Id, ex);
            // 2. Rollback
            setRollback(previousRating);
            _notificationService.Add($"Failed to rate: {ex.Message}", NotificationSeverity.Error);
        }
    }

    public async Task RemoveRatingAsync(BookDto book, Action setOptimistic, Action<int> setRollback,
        CancellationToken cancellationToken = default)
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
            await _booksClient.RemoveBookRatingAsync(book.Id, cancellationToken: cancellationToken);
            _notificationService.Add("Rating removed", NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            Log.RatingRemovalFailed(_logger, book.Id, ex);
            // 2. Rollback
            setRollback(previousRating);
            _notificationService.Add($"Failed to remove rating: {ex.Message}", NotificationSeverity.Error);
        }
    }
}
