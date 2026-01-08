
namespace BookStore.Shared.Messages.Events;

public record BookAddedToFavorites(Guid BookId);
public record BookRemovedFromFavorites(Guid BookId);
