using BookStore.ApiService.Infrastructure; // Added this line
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.Shared.Messages.Events;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace BookStore.ApiService.Handlers;

public static class UserCommandHandler
{
    const int MaxCartQuantity = 10;

    public static async Task Handle(AddBookToFavorites command, IDocumentSession session, HybridCache cache)
    {
        Instrumentation.FavoritesAdded.Add(1, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        // Lazy initialization: If no events exist for this user, initialize the stream
        if (profile == null)
        {
            _ = session.Events.StartStream<UserProfile>(
                command.UserId,
                new UserProfileCreated(command.UserId)
            );
        }

        // Only add if not already in favorites
        // Re-aggregate after potential stream start to get updated profile state for the check
        // Or, more simply, check if profile was null initially OR if it exists and doesn't contain the book
        if (profile == null || !profile.FavoriteBookIds.Contains(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));

            // Invalidate cache
            await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);
        }
    }

    public static async Task Handle(RemoveBookFromFavorites command, IDocumentSession session, HybridCache cache)
    {
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.FavoriteBookIds.Contains(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookRemovedFromFavorites(command.BookId));

            // Invalidate cache
            await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);
        }
    }

    public static async Task Handle(RateBook command, IDocumentSession session, HybridCache cache)
    {
        Instrumentation.RatingsAdded.Add(1, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });

        // Validate rating is between 1-5
        if (command.Rating is < 1 or > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5", nameof(command.Rating));
        }

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        // Lazy initialization: If no events exist for this user, initialize the stream
        if (profile == null)
        {
            _ = session.Events.StartStream<UserProfile>(
                command.UserId,
                new UserProfileCreated(command.UserId)
            );
        }

        // Always append event (either new rating or update)
        // The Apply method will handle updating the existing rating
        _ = session.Events.Append(command.UserId, new BookRated(command.BookId, command.Rating));

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);
    }

    public static async Task Handle(RemoveBookRating command, IDocumentSession session, HybridCache cache)
    {
        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        if (profile != null && profile.BookRatings.ContainsKey(command.BookId))
        {
            _ = session.Events.Append(command.UserId, new BookRatingRemoved(command.BookId));

            // Invalidate cache
            await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);
        }
    }

    public static async Task Handle(AddBookToCart command, IDocumentSession session)
    {
        Instrumentation.CartAdded.Add(1, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });

        if (command.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(command.Quantity));
        }

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        // Lazy initialization: If no events exist for this user, initialize the stream
        if (profile == null)
        {
            _ = session.Events.StartStream<UserProfile>(
                command.UserId,
                new UserProfileCreated(command.UserId)
            );
        }

        // Always append event - Apply method will handle merging quantities
        _ = session.Events.Append(command.UserId, new BookAddedToCart(command.BookId, command.Quantity));
    }

    public static async Task Handle(RemoveBookFromCart command, IDocumentSession session)
    {
        Instrumentation.CartRemoved.Add(1, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);

        // Only append event if book is actually in the cart
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

    public static async Task Handle(MergeAnonymousCart command, IDocumentSession session, ILogger logger)
    {
        if (command.Items.Count == 0)
        {
            Log.Users.AnonymousCartMergeSkipped(logger, command.UserId);
            return;
        }

        var profile = await session.Events.AggregateStreamAsync<UserProfile>(command.UserId);
        if (profile == null)
        {
            _ = session.Events.StartStream<UserProfile>(
                command.UserId,
                new UserProfileCreated(command.UserId)
            );

            profile = new UserProfile { Id = command.UserId };
        }

        var mergedItems = command.Items
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.BookId)
            .Select(group =>
            {
                var currentQuantity = profile.ShoppingCartItems.GetValueOrDefault(group.Key);
                var requestedQuantity = int.Min(group.Sum(item => item.Quantity), MaxCartQuantity);
                var availableQuantity = MaxCartQuantity - currentQuantity;
                var quantityToMerge = int.Min(requestedQuantity, availableQuantity);

                return new AnonymousCartMergedItem(group.Key, int.Max(0, quantityToMerge));
            })
            .Where(item => item.Quantity > 0)
            .ToList();

        if (mergedItems.Count == 0)
        {
            Log.Users.AnonymousCartMergeSkipped(logger, command.UserId);
            return;
        }

        var totalMergedQuantity = mergedItems.Sum(item => item.Quantity);
        Instrumentation.CartAdded.Add(totalMergedQuantity, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });

        _ = session.Events.Append(command.UserId, new AnonymousCartMerged(mergedItems));

        Log.Users.AnonymousCartMerged(logger, command.UserId, mergedItems.Count, totalMergedQuantity);
    }
}
