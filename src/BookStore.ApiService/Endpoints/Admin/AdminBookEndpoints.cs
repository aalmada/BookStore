using System.Collections.Immutable;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.Shared.Models;
using Marten;
using Marten.Linq;
using Marten.Linq.SoftDeletes;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Commands
{
    public record CreateBookRequest(
        string Title,
        string? Isbn,
        string Language,
        IReadOnlyDictionary<string, BookTranslationDto>? Translations,
        PartialDate? PublicationDate,
        Guid? PublisherId,
        IReadOnlyList<Guid> AuthorIds,
        IReadOnlyList<Guid> CategoryIds,
        IReadOnlyDictionary<string, decimal>? Prices = null);

    public record UpdateBookRequest(
        string Title,
        string? Isbn,
        string Language,
        IReadOnlyDictionary<string, BookTranslationDto>? Translations,
        PartialDate? PublicationDate,
        Guid? PublisherId,
        IReadOnlyList<Guid> AuthorIds,
        IReadOnlyList<Guid> CategoryIds,
        IReadOnlyDictionary<string, decimal>? Prices = null);
}

namespace BookStore.ApiService.Endpoints.Admin
{
    public static class AdminBookEndpoints
    {
        public static RouteGroupBuilder MapAdminBookEndpoints(this RouteGroupBuilder group)
        {
            _ = group.MapPost("/", CreateBook)
                .WithName("CreateBook")
                .WithSummary("Create a new book");

            _ = group.MapPut("/{id:guid}", UpdateBook)
                .WithName("UpdateBook")
                .WithSummary("Update a book");

            _ = group.MapDelete("/{id:guid}", SoftDeleteBook)
                .WithName("SoftDeleteBook")
                .WithSummary("Delete a book");

            _ = group.MapPost("/{id:guid}/restore", RestoreBook)
                .WithName("RestoreBook")
                .WithSummary("Restore a deleted book");

            _ = group.MapGet("/", GetAllBooks)
                .WithName("GetAllBooksAdmin")
                .WithSummary("Get all books");

            _ = group.MapPost("/{id:guid}/cover", UploadCover)
                .WithName("UploadBookCover")
                .WithSummary("Upload book cover image")
                .DisableAntiforgery()
                .Accepts<IFormFile>("multipart/form-data");

            return group.RequireAuthorization("Admin");
        }

        // Wolverine approach: Endpoint just creates command and invokes it via message bus
        static Task<IResult> CreateBook(
            [FromBody] Commands.CreateBookRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            CancellationToken cancellationToken)
        {
            var command = new Commands.CreateBook(
                request.Title,
                request.Isbn,
                request.Language,
                request.Translations,
                request.PublicationDate,
                request.PublisherId,
                request.AuthorIds ?? ImmutableList<Guid>.Empty,
                request.CategoryIds ?? ImmutableList<Guid>.Empty,
                request.Prices);

            // Wolverine invokes the handler, manages transaction, and returns result
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> UpdateBook(
            Guid id,
            [FromBody] Commands.UpdateBookRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            // Extract ETag from If-Match header
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();

            var command = new Commands.UpdateBook(
                id,
                request.Title,
                request.Isbn,
                request.Language,
                request.Translations,
                request.PublicationDate,
                request.PublisherId,
                request.AuthorIds ?? ImmutableList<Guid>.Empty,
                request.CategoryIds ?? ImmutableList<Guid>.Empty,
                request.Prices)
            {
                ETag = etag
            };

            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> SoftDeleteBook(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();

            var command = new Commands.SoftDeleteBook(id)
            {
                ETag = etag
            };

            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> RestoreBook(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();

            var command = new Commands.RestoreBook(id)
            {
                ETag = etag
            };

            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        // Read operations don't need Wolverine (no business logic)
        static async Task<IResult> GetAllBooks(
            [FromServices] IQuerySession session,
            CancellationToken cancellationToken)
        {
            var books = await session.Query<Projections.BookSearchProjection>()
                .OrderBy(b => b.Title)
                .ToListAsync(cancellationToken);

            return Results.Ok(books);
        }

        static async Task<IResult> UploadCover(
            Guid id,
            IFormFile file,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            // Validate file
            if (file.Length == 0)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Admin.FileEmpty, "No file uploaded")).ToProblemDetails();
            }

            if (file.Length > 5 * 1024 * 1024) // 5MB limit
            {
                return Result.Failure(Error.Validation(ErrorCodes.Admin.FileTooLarge, "File too large (max 5MB)")).ToProblemDetails();
            }

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Admin.InvalidFileType, "Invalid file type (only JPEG, PNG, WebP allowed)")).ToProblemDetails();
            }

            var etag = context.Request.Headers["If-Match"].FirstOrDefault();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            var content = stream.ToArray();

            var command = new Commands.UpdateBookCover(id, content, file.ContentType)
            {
                ETag = etag
            };

            return await bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }
    }
}
