using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.Shared.Models;
using Marten;
using Marten.Linq;
using Marten.Linq.SoftDeletes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
            // Dictionaries to hold included documents
            var publishers = new Dictionary<Guid, Projections.PublisherProjection>();
            var authors = new Dictionary<Guid, Projections.AuthorProjection>();
            var categories = new Dictionary<Guid, Projections.CategoryProjection>();

#pragma warning disable CS8603 // Possible null reference return - false positive from Marten's Include API
            var books = await session.Query<Projections.BookSearchProjection>()
                .Include(publishers).On(x => x.PublisherId)!
                .Include(authors).On(x => x.AuthorIds)!
                .Include(categories).On(x => x.CategoryIds)!
                .OrderBy(b => b.Title)
                .ToListAsync(cancellationToken);
#pragma warning restore CS8603

            var bookDtos = books.Select(book => new BookDto(
                book.Id,
                book.Title,
                book.Isbn,
                book.OriginalLanguage,
                "", // Lang name
                "", // Description
                book.PublicationDate,
                false,
                book.PublisherId.HasValue && publishers.TryGetValue(book.PublisherId.Value, out var pub)
                    ? new PublisherDto(pub.Id, pub.Name)
                    : null,
                [.. book.AuthorIds
                    .Select(id => authors.TryGetValue(id, out var author)
                        ? new AuthorDto(author.Id, author.Name, "")
                        : null)
                    .Where(a => a != null)
                    .Cast<AuthorDto>()],
                [.. book.CategoryIds
                    .Select(id => categories.TryGetValue(id, out var cat)
                        ? new CategoryDto(cat.Id, "")
                        : null)
                    .Where(c => c != null)
                    .Cast<CategoryDto>()],
                false,
                0, 0f, 0, 0,
                book.Prices.ToDictionary(p => p.Currency, p => p.Value),
                null,
                null,
                book.CurrentPrices,
                book.Deleted
            )).ToList();

            return Results.Ok(bookDtos);
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
