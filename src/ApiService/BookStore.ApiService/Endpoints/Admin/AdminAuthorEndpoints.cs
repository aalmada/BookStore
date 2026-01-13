using Marten;
using Marten.Linq.SoftDeletes;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Commands
{
    public record CreateAuthorRequest(string Name, IReadOnlyDictionary<string, AuthorTranslationDto>? Translations);
    public record UpdateAuthorRequest(string Name, IReadOnlyDictionary<string, AuthorTranslationDto>? Translations);
}

namespace BookStore.ApiService.Endpoints.Admin
{
    public static class AdminAuthorEndpoints
    {
        public static RouteGroupBuilder MapAdminAuthorEndpoints(this RouteGroupBuilder group)
        {
            _ = group.MapPost("/", CreateAuthor)
                .WithName("CreateAuthor")
                .WithSummary("Create a new author");

            _ = group.MapPut("/{id:guid}", UpdateAuthor)
                .WithName("UpdateAuthor")
                .WithSummary("Update an author");

            _ = group.MapDelete("/{id:guid}", SoftDeleteAuthor)
                .WithName("SoftDeleteAuthor")
                .WithSummary("Delete an author");

            _ = group.MapPost("/{id:guid}/restore", RestoreAuthor)
                .WithName("RestoreAuthor")
                .WithSummary("Restore a deleted author");

            _ = group.MapGet("/", GetAllAuthors)
                .WithName("GetAllAuthors")
                .WithSummary("Get all authors (including deleted)");

            return group.RequireAuthorization("Admin");
        }

        static async Task<IResult> GetAllAuthors(
            [FromServices] IQuerySession session,
            CancellationToken cancellationToken)
        {
            var authors = await session.Query<Projections.AuthorProjection>()
                .Where(x => x.MaybeDeleted())
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Results.Ok(authors);
        }

        static Task<IResult> CreateAuthor(
            [FromBody] Commands.CreateAuthorRequest request,
            [FromServices] IMessageBus bus,
            CancellationToken cancellationToken)
        {
            var command = new Commands.CreateAuthor(request.Name, request.Translations);
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> UpdateAuthor(
            Guid id,
            [FromBody] Commands.UpdateAuthorRequest request,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.UpdateAuthor(id, request.Name, request.Translations) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> SoftDeleteAuthor(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeleteAuthor(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> RestoreAuthor(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestoreAuthor(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }
    }
}
