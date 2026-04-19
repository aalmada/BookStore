using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints.Books;

public static class BookCoverEndpoints
{
    public static RouteGroupBuilder MapBookCoverEndpoints(this RouteGroupBuilder group)
    {
        // safe: book cover images are public catalog assets and contain no sensitive data.
        _ = group.MapGet("/{id:guid}/cover", GetBookCover)
            .WithName("GetBookCover")
            .AllowAnonymous();

        return group;
    }

    static async Task<IResult> GetBookCover(
        Guid id,
        [FromQuery] string? tenantId,
        [FromServices] BlobStorageService blobStorage,
        [FromServices] ITenantContext tenantContext,
        [FromServices] ITenantStore tenantStore,
        CancellationToken ct)
    {
        try
        {
            string resolvedTenantId;

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                // Validate the query-supplied tenant against the known tenant list
                // to prevent cross-tenant data probing on anonymous endpoints
                if (!await tenantStore.IsValidTenantAsync(tenantId))
                {
                    return Result.Failure(Error.NotFound(ErrorCodes.Books.BookNotFound, "Book cover not found.")).ToProblemDetails();
                }

                resolvedTenantId = tenantId;
            }
            else
            {
                resolvedTenantId = tenantContext.TenantId;
            }

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
        catch (Exception)
        {
            return Result.Failure(Error.InternalServerError("ERR_BOOK_COVER_FAILED", "Failed to retrieve book cover.")).ToProblemDetails();
        }
    }
}
