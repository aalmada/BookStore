using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface ISystemClient
{
    [Headers("Accept: application/json")]
    [Post("/api/admin/projections/rebuild")]
    Task<RebuildResponse> RebuildProjectionsAsync(CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/projections/status")]
    Task<ProjectionStatusResponse> GetProjectionStatusAsync(CancellationToken cancellationToken = default);
}
