using Marten;
using BookStore.ApiService.Commands.Authors;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Endpoints.Admin;

public static class AdminAuthorEndpoints
{
    public record CreateAuthorRequest(string Name, string? Biography);
    public record UpdateAuthorRequest(string Name, string? Biography);

    public static RouteGroupBuilder MapAdminAuthorEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAuthor)
            .WithName("CreateAuthor")
            .WithSummary("Create a new author using Wolverine command/handler pattern");

        group.MapPut("/{id:guid}", UpdateAuthor)
            .WithName("UpdateAuthor")
            .WithSummary("Update an author");

        group.MapDelete("/{id:guid}", SoftDeleteAuthor)
            .WithName("SoftDeleteAuthor")
            .WithSummary("Soft delete an author");

        group.MapPost("/{id:guid}/restore", RestoreAuthor)
            .WithName("RestoreAuthor")
            .WithSummary("Restore a soft deleted author");

        return group;
    }

    static Task<IResult> CreateAuthor(
        [FromBody] CreateAuthorRequest request,
        [FromServices] IMessageBus bus)
    {
        var command = new CreateAuthor(request.Name, request.Biography);
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> UpdateAuthor(
        Guid id,
        [FromBody] UpdateAuthorRequest request,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new UpdateAuthor(id, request.Name, request.Biography) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> SoftDeleteAuthor(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new SoftDeleteAuthor(id) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> RestoreAuthor(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new RestoreAuthor(id) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }
}
