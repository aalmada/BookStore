using Refit;

namespace BookStore.Client;

[Headers("api-version: 1.0")]
public partial interface IRemoveBookRatingEndpoint
{
    [Delete("/api/books/{id}/rating")]
    Task Execute(Guid id);
}
