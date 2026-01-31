using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetAllPublishersAdminEndpoint
{
    [Get("/api/admin/publishers")]
    Task<PagedListDto<PublisherDto>> GetAllPublishersAsync(
        [Query] PublisherSearchRequest request,
        [Header("Accept-Version")] string version,
        [Header("Accept-Language")] string language,
        CancellationToken cancellationToken = default);
}
