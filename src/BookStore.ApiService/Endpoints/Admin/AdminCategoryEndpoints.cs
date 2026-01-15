using System.Collections.Immutable;
using BookStore.ApiService.Commands;
using Marten;
using Marten.Linq.SoftDeletes;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Commands
{
    public record CreateCategoryRequest(
        IReadOnlyDictionary<string, CategoryTranslationDto>? Translations);

    public record UpdateCategoryRequest(
        IReadOnlyDictionary<string, CategoryTranslationDto>? Translations);
}

namespace BookStore.ApiService.Endpoints.Admin
{
    public static class AdminCategoryEndpoints
    {
        public static RouteGroupBuilder MapAdminCategoryEndpoints(this RouteGroupBuilder group)
        {
            _ = group.MapPost("/", CreateCategory)
                .WithName("CreateCategory")
                .WithSummary("Create a new category");

            _ = group.MapPut("/{id:guid}", UpdateCategory)
                .WithName("UpdateCategory")
                .WithSummary("Update a category");

            _ = group.MapDelete("/{id:guid}", SoftDeleteCategory)
                .WithName("SoftDeleteCategory")
                .WithSummary("Delete a category");

            _ = group.MapPost("/{id:guid}/restore", RestoreCategory)
                .WithName("RestoreCategory")
                .WithSummary("Restore a deleted category");

            _ = group.MapGet("/", GetAllCategories)
                .WithName("GetAllCategories")
                .WithSummary("Get all categories (including deleted)");

            return group.RequireAuthorization("Admin");
        }

        static async Task<IResult> GetAllCategories(
            [FromServices] IQuerySession session,
            CancellationToken cancellationToken)
        {
            var categories = await session.Query<Projections.CategoryProjection>()
                .Where(x => x.MaybeDeleted())
                .OrderBy(x => x.Id) // Categories don't have a single name to sort by easily, ID is safe or we can default sort
                .ToListAsync(cancellationToken);

            return Results.Ok(categories);
        }

        static Task<IResult> CreateCategory(
            [FromBody] Commands.CreateCategoryRequest request,
            [FromServices] IMessageBus bus,
            CancellationToken cancellationToken)
        {
            var translations = request.Translations ?? (IReadOnlyDictionary<string, CategoryTranslationDto>)ImmutableDictionary<string, CategoryTranslationDto>.Empty;
            var command = new Commands.CreateCategory(translations);
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> UpdateCategory(
            Guid id,
            [FromBody] Commands.UpdateCategoryRequest request,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var translations = request.Translations ?? (IReadOnlyDictionary<string, CategoryTranslationDto>)ImmutableDictionary<string, CategoryTranslationDto>.Empty;
            var command = new Commands.UpdateCategory(id, translations) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> SoftDeleteCategory(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeleteCategory(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }

        static Task<IResult> RestoreCategory(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestoreCategory(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, cancellationToken);
        }
    }
}
