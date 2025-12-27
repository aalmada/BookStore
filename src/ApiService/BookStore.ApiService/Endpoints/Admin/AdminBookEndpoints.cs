using Marten;
using BookStore.ApiService.Commands.Books;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Endpoints.Admin;

public static class AdminBookEndpoints
{
    public record CreateBookRequest(
        string Title,
        string? Isbn,
        string? Description,
        DateOnly? PublicationDate,
        Guid? PublisherId,
        List<Guid> AuthorIds,
        List<Guid> CategoryIds);

    public record UpdateBookRequest(
        string Title,
        string? Isbn,
        string? Description,
        DateOnly? PublicationDate,
        Guid? PublisherId,
        List<Guid> AuthorIds,
        List<Guid> CategoryIds);

    public static RouteGroupBuilder MapAdminBookEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateBook)
            .WithName("CreateBook")
            .WithSummary("Create a new book using Wolverine command/handler pattern");

        group.MapPut("/{id:guid}", UpdateBook)
            .WithName("UpdateBook")
            .WithSummary("Update a book. Supports optimistic concurrency with If-Match header.");

        group.MapDelete("/{id:guid}", SoftDeleteBook)
            .WithName("SoftDeleteBook")
            .WithSummary("Soft delete a book. Supports optimistic concurrency with If-Match header.");

        group.MapPost("/{id:guid}/restore", RestoreBook)
            .WithName("RestoreBook")
            .WithSummary("Restore a soft deleted book. Supports optimistic concurrency with If-Match header.");

        group.MapGet("/", GetAllBooks)
            .WithName("GetAllBooksAdmin")
            .WithSummary("Get all books including soft deleted");

        return group;
    }

    // Wolverine approach: Endpoint just creates command and invokes it via message bus
    static Task<IResult> CreateBook(
        [FromBody] CreateBookRequest request,
        [FromServices] IMessageBus bus)
    {
        var command = new CreateBook(
            request.Title,
            request.Isbn,
            request.Description,
            request.PublicationDate,
            request.PublisherId,
            request.AuthorIds ?? [],
            request.CategoryIds ?? []);
        
        // Wolverine invokes the handler, manages transaction, and returns result
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> UpdateBook(
        Guid id,
        [FromBody] UpdateBookRequest request,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        // Extract ETag from If-Match header
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        
        var command = new UpdateBook(
            id,
            request.Title,
            request.Isbn,
            request.Description,
            request.PublicationDate,
            request.PublisherId,
            request.AuthorIds ?? [],
            request.CategoryIds ?? [])
        {
            ETag = etag
        };
        
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> SoftDeleteBook(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        
        var command = new SoftDeleteBook(id)
        {
            ETag = etag
        };
        
        return bus.InvokeAsync<IResult>(command);
    }

    static Task<IResult> RestoreBook(
        Guid id,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        
        var command = new RestoreBook(id)
        {
            ETag = etag
        };
        
        return bus.InvokeAsync<IResult>(command);
    }

    // Read operations don't need Wolverine (no business logic)
    static async Task<IResult> GetAllBooks(
        [FromServices] IQuerySession session)
    {
        var books = await session.Query<Projections.BookSearchProjection>()
            .OrderBy(b => b.Title)
            .ToListAsync();

        return Results.Ok(books);
    }
}
