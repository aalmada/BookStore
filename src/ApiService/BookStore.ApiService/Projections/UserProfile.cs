using BookStore.Shared.Messages.Events;

namespace BookStore.ApiService.Projections;

/// <summary>
/// Event-sourced projection containing user-specific data (cart, favorites, ratings).
/// Separate from ApplicationUser which handles authentication via ASP.NET Core Identity.
/// </summary>
public sealed class UserProfile
{
    /// <summary>
    /// User ID (matches ApplicationUser.Id)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// IDs of books marked as favorite by the user
    /// </summary>
    public ICollection<Guid> FavoriteBookIds { get; set; } = [];

    /// <summary>
    /// Book ratings by the user (BookId → Rating 1-5)
    /// </summary>
    public IDictionary<Guid, int> BookRatings { get; set; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Shopping cart items (BookId → Quantity)
    /// </summary>
    public IDictionary<Guid, int> ShoppingCartItems { get; set; } = new Dictionary<Guid, int>();

    // Apply methods for Marten Self-Aggregating Snapshot
    public void Apply(BookAddedToFavorites @event)
    {
        if (!FavoriteBookIds.Contains(@event.BookId))
        {
            FavoriteBookIds.Add(@event.BookId);
        }
    }

    public void Apply(BookRemovedFromFavorites @event)
    {
        if (FavoriteBookIds.Contains(@event.BookId))
        {
            _ = FavoriteBookIds.Remove(@event.BookId);
        }
    }

    public void Apply(BookRated @event)
        => BookRatings[@event.BookId] = @event.Rating;

    public void Apply(BookRatingRemoved @event)
        => _ = BookRatings.Remove(@event.BookId);

    public void Apply(BookAddedToCart @event)
    {
        if (ShoppingCartItems.ContainsKey(@event.BookId))
        {
            ShoppingCartItems[@event.BookId] += @event.Quantity;
        }
        else
        {
            ShoppingCartItems[@event.BookId] = @event.Quantity;
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public void Apply(BookRemovedFromCart @event)
        => _ = ShoppingCartItems.Remove(@event.BookId);

    public void Apply(CartItemQuantityUpdated @event)
        => ShoppingCartItems[@event.BookId] = @event.Quantity;

    public void Apply(ShoppingCartCleared @event)
        => ShoppingCartItems.Clear();
#pragma warning restore IDE0060 // Remove unused parameter
}
