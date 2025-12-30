using Marten;
using Marten.Events.Daemon;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints.Admin;

public record RebuildResponse(string Message);
public record ProjectionStatusResponse(string Status, string Message);

public static class ProjectionEndpoints
{
    public static RouteGroupBuilder MapProjectionEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/rebuild", RebuildProjections)
            .WithName("RebuildProjections")
            .WithSummary("Rebuild all projections");

        _ = group.MapGet("/status", GetProjectionStatus)
            .WithName("GetProjectionStatus")
            .WithSummary("Get projection status");

        return group;
    }

    static async Task<Ok<RebuildResponse>> RebuildProjections(
        [FromServices] IDocumentStore store)
    {
        // Rebuild all async projections
        var daemon = await store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<Projections.BookSearchProjection>(CancellationToken.None);
        await daemon.RebuildProjectionAsync<Projections.AuthorProjection>(CancellationToken.None);
        await daemon.RebuildProjectionAsync<Projections.CategoryProjection>(CancellationToken.None);
        await daemon.RebuildProjectionAsync<Projections.PublisherProjection>(CancellationToken.None);

        return TypedResults.Ok(new RebuildResponse("Projection rebuild initiated"));
    }

    static async Task<Ok<ProjectionStatusResponse>> GetProjectionStatus(
        [FromServices] IDocumentStore store)
    {
        try
        {
            var daemon = await store.BuildProjectionDaemonAsync();

            // Return basic daemon status
            return TypedResults.Ok(new ProjectionStatusResponse(
                "running",
                "Projection daemon is active. Use rebuild endpoint to refresh projections."));
        }
        catch (Exception ex)
        {
            return TypedResults.Ok(new ProjectionStatusResponse(
                "error",
                ex.Message));
        }
    }
}
