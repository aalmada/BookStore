using Refit;

namespace BookStore.Client;

[Headers("api-version: 1.0")]
public interface IShoppingCartClient
{
    [Get("/api/cart")]
    Task<ShoppingCartResponse> GetShoppingCartAsync(CancellationToken cancellationToken = default);

    [Post("/api/cart/items")]
    Task AddToCartAsync([Body] AddToCartClientRequest request, CancellationToken cancellationToken = default);

    [Put("/api/cart/items/{bookId}")]
    Task UpdateCartItemAsync(Guid bookId, [Body] UpdateCartItemClientRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/cart/items/{bookId}")]
    Task RemoveFromCartAsync(Guid bookId, CancellationToken cancellationToken = default);

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
