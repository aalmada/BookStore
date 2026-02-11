using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing the user's shopping cart.
/// </summary>
[Headers("api-version: 1.0")]
public interface IShoppingCartClient
{
    /// <summary>
    /// Gets the current user's shopping cart.
    /// </summary>

    [Get("/api/cart")]
    Task<ShoppingCartResponse> GetShoppingCartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a book to the shopping cart.
    /// </summary>

    [Post("/api/cart/items")]
    Task AddToCartAsync([Body] AddToCartClientRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the quantity of a book in the shopping cart.
    /// </summary>

    [Put("/api/cart/items/{bookId}")]
    Task UpdateCartItemAsync(Guid bookId, [Body] UpdateCartItemClientRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book from the shopping cart.
    /// </summary>

    [Delete("/api/cart/items/{bookId}")]
    Task RemoveFromCartAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all items from the shopping cart.
    /// </summary>

    [Delete("/api/cart")]
    Task ClearCartAsync(CancellationToken cancellationToken = default);
}

public record ShoppingCartResponse(
    List<ShoppingCartItemResponse> Items,
    int TotalItems);

public record ShoppingCartItemResponse(
    Guid BookId,
    string Title,
    string? Isbn,
    int Quantity,
    IReadOnlyDictionary<string, decimal> Prices);

public record AddToCartClientRequest(Guid BookId, int Quantity);
public record UpdateCartItemClientRequest(int Quantity);
