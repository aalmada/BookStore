namespace BookStore.ApiService.Messages.Commands;

public record AddToCartRequest(Guid BookId, int Quantity);
public record UpdateCartItemRequest(int Quantity);
