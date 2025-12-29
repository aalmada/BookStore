using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Commands
{
    public record CreateCategoryRequest(
        Dictionary<string, CategoryTranslationDto>? Translations);

    public record UpdateCategoryRequest(
        Dictionary<string, CategoryTranslationDto>? Translations);
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

            return group;
        }

        static Task<IResult> CreateCategory(
            [FromBody] Commands.CreateCategoryRequest request,
            [FromServices] IMessageBus bus)
        {
            var translations = request.Translations ?? [];
            var command = new Commands.CreateCategory(translations);
            return bus.InvokeAsync<IResult>(command);
        }

        static Task<IResult> UpdateCategory(
            Guid id,
            [FromBody] Commands.UpdateCategoryRequest request,
            [FromServices] IMessageBus bus,
            HttpContext context)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var translations = request.Translations ?? [];
            var command = new Commands.UpdateCategory(id, translations) { ETag = etag };
            return bus.InvokeAsync<IResult>(command);
        }

        static Task<IResult> SoftDeleteCategory(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeleteCategory(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command);
        }

        static Task<IResult> RestoreCategory(
            Guid id,
            [FromServices] IMessageBus bus,
            HttpContext context)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestoreCategory(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command);
        }
    }
}
