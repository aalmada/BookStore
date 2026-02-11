using System.Globalization;
using System.Linq;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
using Marten;
using Marten.Linq.SoftDeletes;
using Marten.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wolverine;

namespace BookStore.ApiService.Commands
{
    public record CreateAuthorRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public Guid Id { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; init; } = default!;
        [System.Text.Json.Serialization.JsonPropertyName("translations")] public IReadOnlyDictionary<string, AuthorTranslationDto>? Translations { get; init; }
    }
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

            _ = group.MapGet("/{id:guid}", GetAuthor)
                .WithName("GetAdminAuthor")
                .WithSummary("Get author by ID (including deleted)");

            return group.RequireAuthorization("Admin");
        }

        static async Task<IResult> GetAllAuthors(
            [FromServices] IQuerySession session,
            [FromServices] IOptions<PaginationOptions> paginationOptions,
            [FromServices] IOptions<LocalizationOptions> localizationOptions,
            [AsParameters] AuthorSearchRequest request,
            CancellationToken cancellationToken)
        {
            var paging = request.Normalize(paginationOptions.Value);
            var culture = CultureInfo.CurrentUICulture.Name;
            var defaultCulture = localizationOptions.Value.DefaultCulture;

            var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
            var normalizedSortBy = request.SortBy?.ToLowerInvariant();

            // Use IQueryable to avoid compilation errors when reassigning after standard LINQ operators
            IQueryable<AuthorProjection> query = session.Query<AuthorProjection>();

            // Admin specifically wants to see both deleted and non-deleted
            // Since Marten native soft-delete is not enabled, session.Query returns all by default.

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(search));
            }

            query = (normalizedSortBy, normalizedSortOrder) switch
            {
                ("id", "desc") => query.OrderByDescending(x => x.Id),
                ("id", "asc") => query.OrderBy(x => x.Id),
                ("name", "desc") => query.OrderByDescending(x => x.Name),
                _ => query.OrderBy(x => x.Name)
            };

            var pagedList = await query.ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancellationToken);

            // Mapping MUST happen on the client side (after ToList()) because Marten 
            // cannot translate complex Dictionary access or helper methods in Select()
            var dtos = pagedList.ToList().Select(x => new AdminAuthorDto(
                x.Id,
                x.Name,
                LocalizationHelper.GetLocalizedValue(x.Biographies, culture, defaultCulture, ""),
                x.Biographies.ToDictionary(kvp => kvp.Key, kvp => new AuthorTranslationDto(kvp.Value)),
                ETagHelper.GenerateETag(x.Version)
            )).ToList();

            return Results.Ok(new PagedListDto<AdminAuthorDto>(dtos, pagedList.PageNumber, pagedList.PageSize, pagedList.TotalItemCount));
        }

        static async Task<IResult> GetAuthor(
            Guid id,
            [FromServices] IQuerySession session,
            [FromServices] IOptions<LocalizationOptions> localizationOptions,
            CancellationToken cancellationToken)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            var defaultCulture = localizationOptions.Value.DefaultCulture;

            var author = await session.LoadAsync<AuthorProjection>(id, cancellationToken);
            if (author == null)
            {
                return Results.NotFound();
            }

            var dto = new AdminAuthorDto(
                author.Id,
                author.Name,
                LocalizationHelper.GetLocalizedValue(author.Biographies, culture, defaultCulture, ""),
                author.Biographies.ToDictionary(kvp => kvp.Key, kvp => new AuthorTranslationDto(kvp.Value)),
                ETagHelper.GenerateETag(author.Version)
            );

            return Results.Ok(dto).WithETag(dto.ETag!);
        }

        static Task<IResult> CreateAuthor(
            [FromBody] Commands.CreateAuthorRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            CancellationToken cancellationToken)
        {
            var command = new Commands.CreateAuthor(request.Id, request.Name, request.Translations);
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> UpdateAuthor(
            Guid id,
            [FromBody] Commands.UpdateAuthorRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.UpdateAuthor(id, request.Name, request.Translations) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> SoftDeleteAuthor(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeleteAuthor(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> RestoreAuthor(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestoreAuthor(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }
    }
}
