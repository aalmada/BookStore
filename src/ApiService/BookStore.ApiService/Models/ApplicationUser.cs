using Microsoft.AspNetCore.Identity;

namespace BookStore.ApiService.Models;

/// <summary>
/// Represents a user in the BookStore application.
/// Stored as a document in Marten/PostgreSQL.
/// </summary>
public sealed class ApplicationUser
{
    /// <summary>
    /// Unique identifier for the user (Version 7 GUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// User's username
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Normalized username for case-insensitive lookups
    /// </summary>
    public string? NormalizedUserName { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Normalized email for case-insensitive lookups
    /// </summary>
    public string? NormalizedEmail { get; set; }

    /// <summary>
    /// Indicates whether the email has been confirmed
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Hashed password
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Security stamp for invalidating tokens when credentials change
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.CreateVersion7().ToString();

    /// <summary>
    /// Concurrency stamp for optimistic concurrency control
    /// </summary>
    public string ConcurrencyStamp { get; set; } = Guid.CreateVersion7().ToString();

    /// <summary>
    /// Roles assigned to this user
    /// </summary>
    public ICollection<string> Roles { get; set; } = [];

    /// <summary>
    /// IDs of books marked as favorite by the user
    /// </summary>
    public ICollection<Guid> FavoriteBookIds { get; set; } = [];

    /// <summary>
    /// Book ratings by the user (BookId -> Rating 1-5)
    /// </summary>
    public IDictionary<Guid, int> BookRatings { get; set; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Shopping cart items (BookId -> Quantity)
    /// </summary>
    public IDictionary<Guid, int> ShoppingCartItems { get; set; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Passkeys registered to this user (WebAuthn/FIDO2)
    /// </summary>
    public IList<UserPasskeyInfo> Passkeys { get; set; } = [];

    /// <summary>
    /// Refresh tokens for maintaining sessions
    /// </summary>
    public IList<RefreshTokenInfo> RefreshTokens { get; set; } = [];

    // Apply methods for Marten Self-Aggregating Snapshot
    public void Apply(BookStore.Shared.Messages.Events.BookAddedToFavorites @event)
    {
        if (!FavoriteBookIds.Contains(@event.BookId))
        {
            FavoriteBookIds.Add(@event.BookId);
        }
    }

    public void Apply(BookStore.Shared.Messages.Events.BookRemovedFromFavorites @event)
    {
        if (FavoriteBookIds.Contains(@event.BookId))
        {
            _ = FavoriteBookIds.Remove(@event.BookId);
        }
    }

    public void Apply(BookStore.Shared.Messages.Events.BookRated @event)
        => BookRatings[@event.BookId] = @event.Rating;

    public void Apply(BookStore.Shared.Messages.Events.BookRatingRemoved @event)
        => _ = BookRatings.Remove(@event.BookId);

    public void Apply(BookStore.Shared.Messages.Events.BookAddedToCart @event)
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

    public void Apply(BookStore.Shared.Messages.Events.BookRemovedFromCart @event)
        => _ = ShoppingCartItems.Remove(@event.BookId);

    public void Apply(BookStore.Shared.Messages.Events.CartItemQuantityUpdated @event)
        => ShoppingCartItems[@event.BookId] = @event.Quantity;

    public void Apply(BookStore.Shared.Messages.Events.ShoppingCartCleared @event)
        => ShoppingCartItems.Clear();
}

/// <summary>
/// Information about a refresh token
/// </summary>
public record RefreshTokenInfo(
    string Token,
    DateTimeOffset Expires,
    DateTimeOffset Created);
