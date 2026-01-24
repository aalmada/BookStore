using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints.Books;

public static class BookCoverEndpoints
{
    public static RouteGroupBuilder MapBookCoverEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/{id:guid}/cover", GetBookCover)
            .WithName("GetBookCover")
            .AllowAnonymous(); // Public access covers

        return group;
    }

    static async Task<IResult> GetBookCover(
        Guid id,
        [FromQuery] string? tenantId,
        [FromServices] BlobStorageService blobStorage,
        [FromServices] ITenantContext tenantContext,
        CancellationToken ct)
    {
        try
        {
            // If tenantId is provided in query (e.g. from <img> tag), use it.
            // Otherwise fall back to the resolved tenant context (e.g. host header).
            var resolvedTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : tenantContext.TenantId;

            var result = await blobStorage.GetBookCoverAsync(id, resolvedTenantId, ct);

            return Results.File(
                result.Content.ToStream(),
                result.Details.ContentType,
                enableRangeProcessing: true // Support range requests (optional but good for media)
            );
        }
        catch (FileNotFoundException)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Books.BookNotFound, "Book cover not found.")).ToProblemDetails();
        }
        catch (Exception ex)
        {
            // Log?
            return Result.Failure(Error.InternalServerError("ERR_BOOK_COVER_FAILED", ex.Message)).ToProblemDetails();
        }
    }
}
