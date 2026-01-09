using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.Shared.Messages.Events;
using Marten;
using Wolverine;

namespace BookStore.ApiService.Handlers;

public static class UserCommandHandler
{
    public static async Task Handle(AddBookToFavorites command, IDocumentSession session)
    {
        // Load the user profile stream to check current state
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && !profile.FavoriteBookIds.Contains(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));
        }
        else if (profile == null)
        {
            // First event for this user profile stream
            _ = session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));
        }
    }

    public static async Task Handle(RemoveBookFromFavorites command, IDocumentSession session)
    {
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.FavoriteBookIds.Contains(command.BookId))
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

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        // Always append event (either new rating or update)
        // The Apply method will handle updating the existing rating
        _ = session.Events.Append(command.UserId, new BookRated(command.BookId, command.Rating));
    }

    public static async Task Handle(RemoveBookRating command, IDocumentSession session)
    {
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.BookRatings.ContainsKey(command.BookId))
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

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        // Always append event - Apply method will handle merging quantities
        _ = session.Events.Append(command.UserId, new BookAddedToCart(command.BookId, command.Quantity));
    }

    public static async Task Handle(RemoveBookFromCart command, IDocumentSession session)
    {
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.ShoppingCartItems.ContainsKey(command.BookId))
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

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.ShoppingCartItems.ContainsKey(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new CartItemQuantityUpdated(command.BookId, command.Quantity));
        }
    }

    public static async Task Handle(ClearShoppingCart command, IDocumentSession session)
    {
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.ShoppingCartItems.Count > 0)
        {
            _ = session.Events.Append(command.UserId, new ShoppingCartCleared());
        }
    }
}
