using Marten;
using Marten.Linq.SoftDeletes;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Commands
{
    public record CreatePublisherRequest(string Name);
    public record UpdatePublisherRequest(string Name);
}

namespace BookStore.ApiService.Endpoints.Admin
{
    public static class AdminPublisherEndpoints
    {
        public static RouteGroupBuilder MapAdminPublisherEndpoints(this RouteGroupBuilder group)
        {
            _ = group.MapPost("/", CreatePublisher)
                .WithName("CreatePublisher")
                .WithSummary("Create a new publisher");

            _ = group.MapPut("/{id:guid}", UpdatePublisher)
                .WithName("UpdatePublisher")
                .WithSummary("Update a publisher");

            _ = group.MapDelete("/{id:guid}", SoftDeletePublisher)
                .WithName("SoftDeletePublisher")
                .WithSummary("Delete a publisher");

            _ = group.MapPost("/{id:guid}/restore", RestorePublisher)
                .WithName("RestorePublisher")
                .WithSummary("Restore a deleted publisher");

            _ = group.MapGet("/", GetAllPublishers)
                .WithName("GetAllPublishers")
                .WithSummary("Get all publishers (including deleted)");

            return group.RequireAuthorization("Admin");
        }

        static async Task<IResult> GetAllPublishers(
            [FromServices] IQuerySession session,
            CancellationToken cancellationToken)
        {
            var publishers = await session.Query<Projections.PublisherProjection>()
                .Where(x => x.MaybeDeleted())
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Results.Ok(publishers);
        }

        static Task<IResult> CreatePublisher(
            [FromBody] Commands.CreatePublisherRequest request,
            [FromServices] IMessageBus bus,
            CancellationToken cancellationToken)
        {
            var command = new Commands.CreatePublisher(request.Name);
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> UpdatePublisher(
            Guid id,
            [FromBody] Commands.UpdatePublisherRequest request,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.UpdatePublisher(id, request.Name) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> SoftDeletePublisher(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeletePublisher(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> RestorePublisher(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestorePublisher(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }
    }
}
