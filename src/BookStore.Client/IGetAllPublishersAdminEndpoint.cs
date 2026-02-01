using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetAllPublishersAdminEndpoint
{
    [Get("/api/admin/publishers")]
    Task<PagedListDto<PublisherDto>> GetAllPublishersAsync(
        [Query] PublisherSearchRequest request,
        CancellationToken cancellationToken = default);
}
