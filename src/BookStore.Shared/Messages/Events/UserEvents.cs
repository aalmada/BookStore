
namespace BookStore.Shared.Messages.Events;

public record BookAddedToFavorites(Guid BookId);
public record BookRemovedFromFavorites(Guid BookId);

public record BookRated(Guid BookId, int Rating);
public record BookRatingRemoved(Guid BookId);

public record BookAddedToCart(Guid BookId, int Quantity);
public record BookRemovedFromCart(Guid BookId);
public record CartItemQuantityUpdated(Guid BookId, int Quantity);
public record ShoppingCartCleared();
