using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace BookStore.Client;

public interface IRestoreBookEndpoint
{
    [Post("/api/admin/books/{id}/restore")]
    Task RestoreBookAsync(Guid id, [Header("api-version")] string apiVersion = "1.0", CancellationToken cancellationToken = default);
}
