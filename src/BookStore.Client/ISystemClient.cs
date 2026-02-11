using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for system-level operations (Admin only).
/// </summary>
public interface ISystemClient
{
    /// <summary>
    /// Rebuilds all read projections.
    /// </summary>

    [Headers("Accept: application/json")]
    [Post("/api/admin/projections/rebuild")]
    Task<RebuildResponse> RebuildProjectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of system projections.
    /// </summary>

    [Headers("Accept: application/json")]
    [Get("/api/admin/projections/status")]
    Task<ProjectionStatusResponse> GetProjectionStatusAsync(CancellationToken cancellationToken = default);
}
