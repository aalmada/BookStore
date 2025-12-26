using Marten;
using BookStore.ApiService.Commands.Publishers;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Endpoints.Admin;

public static class AdminPublisherEndpoints
{
    public record CreatePublisherRequest(string Name);
    public record UpdatePublisherRequest(string Name);

    public static RouteGroupBuilder MapAdminPublisherEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreatePublisher)
            .WithName("CreatePublisher")
            .WithSummary("Create a new publisher using Wolverine command/handler pattern");

        group.MapPut("/{id:guid}", UpdatePublisher)
            .WithName("UpdatePublisher")
            .WithSummary("Update a publisher");

        group.MapDelete("/{id:guid}", SoftDeletePublisher)
            .WithName("SoftDeletePublisher")
            .WithSummary("Soft delete a publisher");

        group.MapPost("/{id:guid}/restore", RestorePublisher)
            .WithName("RestorePublisher")
            .WithSummary("Restore a soft deleted publisher");

        return group;
    }

    static Task<IResult> CreatePublisher(
        [FromBody] CreatePublisherRequest request,
        [FromServices] IMessageBus bus)
    {
        var command = new CreatePublisher(request.Name);
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> UpdatePublisher(
        Guid id,
        [FromBody] UpdatePublisherRequest request,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new UpdatePublisher(id, request.Name) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> SoftDeletePublisher(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new SoftDeletePublisher(id) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> RestorePublisher(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new RestorePublisher(id) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }
}
