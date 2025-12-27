using Marten;
using BookStore.ApiService.Commands.Categories;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Endpoints.Admin;

public static class AdminCategoryEndpoints
{
    public record CreateCategoryRequest(
        string Name, 
        string? Description,
        Dictionary<string, CategoryTranslationDto>? Translations);
    
    public record UpdateCategoryRequest(
        string Name, 
        string? Description,
        Dictionary<string, CategoryTranslationDto>? Translations);

    public static RouteGroupBuilder MapAdminCategoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateCategory)
            .WithName("CreateCategory")
            .WithSummary("Create a new category with optional translations using Wolverine");

        group.MapPut("/{id:guid}", UpdateCategory)
            .WithName("UpdateCategory")
            .WithSummary("Update a category and its translations");

        group.MapDelete("/{id:guid}", SoftDeleteCategory)
            .WithName("SoftDeleteCategory")
            .WithSummary("Soft delete a category");

        group.MapPost("/{id:guid}/restore", RestoreCategory)
            .WithName("RestoreCategory")
            .WithSummary("Restore a soft deleted category");

        return group;
    }

    static Task<IResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        [FromServices] IMessageBus bus)
    {
        var translations = request.Translations ?? [];
        var command = new CreateCategory(request.Name, request.Description, translations);
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var translations = request.Translations ?? [];
        var command = new UpdateCategory(id, request.Name, request.Description, translations) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> SoftDeleteCategory(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new SoftDeleteCategory(id) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> RestoreCategory(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        var command = new RestoreCategory(id) { ETag = etag };
        return bus.InvokeAsync<IResult>(command);
    }
}
