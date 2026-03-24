namespace BookStore.Shared.Models;

public record MergeCartRequest(List<CartItemToMergeRequest> Items);
public record CartItemToMergeRequest(Guid BookId, int Quantity);
