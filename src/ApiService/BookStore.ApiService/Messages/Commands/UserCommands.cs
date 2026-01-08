
namespace BookStore.ApiService.Messages.Commands;

public record AddBookToFavorites(Guid UserId, Guid BookId);
public record RemoveBookFromFavorites(Guid UserId, Guid BookId);
