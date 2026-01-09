
namespace BookStore.Shared.Messages.Events;

public record BookAddedToFavorites(Guid BookId);
public record BookRemovedFromFavorites(Guid BookId);

public record BookRated(Guid BookId, int Rating);
public record BookRatingRemoved(Guid BookId);
