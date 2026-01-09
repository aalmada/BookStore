namespace BookStore.ApiService.Models;

public record ShoppingCartDto(
    List<ShoppingCartItemDto> Items,
    int TotalItems);

public record ShoppingCartItemDto(
    Guid BookId,
    string Title,
    string? Isbn,
    int Quantity);
