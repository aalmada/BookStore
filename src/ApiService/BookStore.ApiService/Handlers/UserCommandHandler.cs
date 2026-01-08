using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.Shared.Messages.Events;
using Marten;
using Wolverine;

namespace BookStore.ApiService.Handlers;

public static class UserCommandHandler
{
    public static async Task Handle(AddBookToFavorites command, IDocumentSession session)
    {
        // Load the user stream to check current state (if needed for idempotency in business logic)
        // Or aggregate it from the stream
        var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && !user.FavoriteBookIds.Contains(command.BookId))
        {
            session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));
            // No need to save changes, Wolverine + Marten integration handles it if configured
            // But usually we return the event or explicitly append
        }
        else if (user == null)
        {
            // Case where user doesn't exist? This shouldn't happen for authenticated valid users
            // But if it's a new user stream, we might need to be careful.
            // Marten Identity users are documents, but here we are appending to a stream with the same ID.
            // If the stream doesn't exist, it will start one.
            // The Inline Projection will then create/update the document.
            // HOWEVER: ApplicationUser document ALREADY exists (via Identity).
            // We need to ensure that appending events to this stream ID will UPDATING the existing document helper
            // via the Snapshot<ApplicationUser>(Inline) we registered.
            session.Events.Append(command.UserId, new BookAddedToFavorites(command.BookId));
        }
    }

    public static async Task Handle(RemoveBookFromFavorites command, IDocumentSession session)
    {
         var user = await session.Events.AggregateStreamAsync<ApplicationUser>(command.UserId);

        if (user != null && user.FavoriteBookIds.Contains(command.BookId))
        {
            session.Events.Append(command.UserId, new BookRemovedFromFavorites(command.BookId));
        }
    }
}
