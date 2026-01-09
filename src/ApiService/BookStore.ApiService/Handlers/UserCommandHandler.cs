using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.Shared.Messages.Events;
using Marten;
using Wolverine;

namespace BookStore.ApiService.Handlers;

public static class UserCommandHandler
{
    public static async Task Handle(AddBookToFavorites command, IDocumentSession session)
    {
        // Load the user stream to check current state (if needed for idempotency in business logic)
        // Or aggregate it from the stream
        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && !user.FavoriteBookIds.Contains(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));
            // No need to save changes, Wolverine + Marten integration handles it if configured
            // But usually we return the event or explicitly append
        }
        else if (user == null)
        {
            // Case where user doesn't exist? This shouldn't happen for authenticated valid users
            // But if it's a new user stream, we might need to be careful.
            // Marten Identity users are documents, but here we are appending to a stream with the same ID.
            // If the stream doesn't exist, it will start one.
            // The Inline Projection will then create/update the document.
            // HOWEVER: ApplicationUser document ALREADY exists (via Identity).
            // We need to ensure that appending events to this stream ID will UPDATING the existing document helper
            // via the Snapshot<ApplicationUser>(Inline) we registered.
            _ = session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));
        }
    }

    public static async Task Handle(RemoveBookFromFavorites command, IDocumentSession session)
    {
        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && user.FavoriteBookIds.Contains(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookRemovedFromFavorites(command.BookId));
        }
    }

    public static async Task Handle(RateBook command, IDocumentSession session)
    {
        // Validate rating is between 1-5
        if (command.Rating is < 1 or > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5", nameof(command.Rating));
        }

        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        // Always append event (either new rating or update)
        // The Apply method will handle updating the existing rating
        _ = session.Events.Append(command.UserId, new BookRated(command.BookId, command.Rating));
    }

    public static async Task Handle(RemoveBookRating command, IDocumentSession session)
    {
        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && user.BookRatings.ContainsKey(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookRatingRemoved(command.BookId));
        }
    }

    public static async Task Handle(AddBookToCart command, IDocumentSession session)
    {
        if (command.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(command.Quantity));
        }

        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        // Always append event - Apply method will handle merging quantities
        _ = session.Events.Append(command.UserId, new BookAddedToCart(command.BookId, command.Quantity));
    }

    public static async Task Handle(RemoveBookFromCart command, IDocumentSession session)
    {
        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && user.ShoppingCartItems.ContainsKey(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookRemovedFromCart(command.BookId));
        }
    }

    public static async Task Handle(UpdateCartItemQuantity command, IDocumentSession session)
    {
        if (command.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(command.Quantity));
        }

        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && user.ShoppingCartItems.ContainsKey(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new CartItemQuantityUpdated(command.BookId, command.Quantity));
        }
    }

    public static async Task Handle(ClearShoppingCart command, IDocumentSession session)
    {
        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && user.ShoppingCartItems.Count > 0)
        {
            _ = session.Events.Append(command.UserId, new ShoppingCartCleared());
        }
    }
}
