
namespace BookStore.ApiService.Messages.Commands;

public record AddBookToFavorites(Guid UserId, Guid BookId);
public record RemoveBookFromFavorites(Guid UserId, Guid BookId);

public record RateBook(Guid UserId, Guid BookId, int Rating);
public record RemoveBookRating(Guid UserId, Guid BookId);

public record AddBookToCart(Guid UserId, Guid BookId, int Quantity);
public record RemoveBookFromCart(Guid UserId, Guid BookId);
public record UpdateCartItemQuantity(Guid UserId, Guid BookId, int Quantity);
public record ClearShoppingCart(Guid UserId);
