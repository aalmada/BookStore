using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Projections;
using Marten;
using Marten.Linq.SoftDeletes;
using Marten.Pagination;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
            [FromServices] IOptions<PaginationOptions> paginationOptions,
            [FromServices] IOptions<LocalizationOptions> localizationOptions,
            [AsParameters] CategorySearchRequest request,
            CancellationToken cancellationToken)
        {
            var paging = request.Normalize(paginationOptions.Value);
            var culture = CultureInfo.CurrentUICulture.Name;
            var defaultCulture = localizationOptions.Value.DefaultCulture;

            var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
            var normalizedSortBy = request.SortBy?.ToLowerInvariant();

            IQueryable<CategoryProjection> query = session.Query<CategoryProjection>();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.ToLower();
                // Simpler dictionary search, though still might be problematic for Marten 
                // on projections. We'll try this first.
                query = query.Where(x => x.Names.Values.Any(v => v.Contains(request.Search, StringComparison.OrdinalIgnoreCase)));
            }

            query = (normalizedSortBy, normalizedSortOrder) switch
            {
                ("id", "desc") => query.OrderByDescending(x => x.Id),
                _ => query.OrderBy(x => x.Id)
            };

            var pagedList = await query.ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, cancellationToken);

            var dtos = pagedList.ToList().Select(x => new AdminCategoryDto(
                x.Id,
                LocalizationHelper.GetLocalizedValue(x.Names, culture, defaultCulture, "Unknown"),
                x.Names.ToDictionary(kvp => kvp.Key, kvp => new CategoryTranslationDto(kvp.Value))
            )).ToList();

            return Results.Ok(new PagedListDto<AdminCategoryDto>(dtos, pagedList.PageNumber, pagedList.PageSize, pagedList.TotalItemCount));
        }

        static Task<IResult> CreateCategory(
            [FromBody] Commands.CreateCategoryRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            CancellationToken cancellationToken)
        {
            var translations = request.Translations ?? (IReadOnlyDictionary<string, CategoryTranslationDto>)ImmutableDictionary<string, CategoryTranslationDto>.Empty;
            var command = new Commands.CreateCategory(translations);
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> UpdateCategory(
            Guid id,
            [FromBody] Commands.UpdateCategoryRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var translations = request.Translations ?? (IReadOnlyDictionary<string, CategoryTranslationDto>)ImmutableDictionary<string, CategoryTranslationDto>.Empty;
            var command = new Commands.UpdateCategory(id, translations) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> SoftDeleteCategory(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeleteCategory(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> RestoreCategory(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestoreCategory(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }
    }
}
