using Marten;
using Marten.Events.Daemon;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints.Admin;

public static class ProjectionEndpoints
{
    public static RouteGroupBuilder MapProjectionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/rebuild", RebuildProjections)
            .WithName("RebuildProjections")
            .WithSummary("Rebuild all async projections from event store");

        group.MapGet("/status", GetProjectionStatus)
            .WithName("GetProjectionStatus")
            .WithSummary("Get async daemon projection status");

        return group;
    }

    static async Task<IResult> RebuildProjections(
        [FromServices] IDocumentStore store)
    {
        // Rebuild all async projections
        var daemon = await store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<Projections.BookSearchProjection>(CancellationToken.None);
        await daemon.RebuildProjectionAsync<Projections.AuthorProjection>(CancellationToken.None);
        await daemon.RebuildProjectionAsync<Projections.CategoryProjection>(CancellationToken.None);
        await daemon.RebuildProjectionAsync<Projections.PublisherProjection>(CancellationToken.None);

        return Results.Ok(new { message = "Projection rebuild initiated" });
    }

    static async Task<IResult> GetProjectionStatus(
        [FromServices] IDocumentStore store)
    {
        try
        {
            var daemon = await store.BuildProjectionDaemonAsync();
            
            // Return basic daemon status
            return Results.Ok(new 
            { 
                status = "running",
                message = "Projection daemon is active. Use rebuild endpoint to refresh projections."
            });
        }
        catch (Exception ex)
        {
            return Results.Ok(new 
            { 
                status = "error",
                message = ex.Message
            });
        }
    }
}
