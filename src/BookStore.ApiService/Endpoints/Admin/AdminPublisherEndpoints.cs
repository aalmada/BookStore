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
    public record CreatePublisherRequest(string Name);
    public record UpdatePublisherRequest(string Name);
}

namespace BookStore.ApiService.Endpoints.Admin
{
    public static class AdminPublisherEndpoints
    {
        public static RouteGroupBuilder MapAdminPublisherEndpoints(this RouteGroupBuilder group)
        {
            _ = group.MapPost("/", CreatePublisher)
                .WithName("CreatePublisher")
                .WithSummary("Create a new publisher");

            _ = group.MapPut("/{id:guid}", UpdatePublisher)
                .WithName("UpdatePublisher")
                .WithSummary("Update a publisher");

            _ = group.MapDelete("/{id:guid}", SoftDeletePublisher)
                .WithName("SoftDeletePublisher")
                .WithSummary("Delete a publisher");

            _ = group.MapPost("/{id:guid}/restore", RestorePublisher)
                .WithName("RestorePublisher")
                .WithSummary("Restore a deleted publisher");

            _ = group.MapGet("/", GetAllPublishers)
                .WithName("GetAllPublishers")
                .WithSummary("Get all publishers (including deleted)");

            return group.RequireAuthorization("Admin");
        }

        static async Task<IResult> GetAllPublishers(
            [FromServices] IQuerySession session,
            [FromServices] IOptions<PaginationOptions> paginationOptions,
            [AsParameters] PublisherSearchRequest request,
            CancellationToken cancellationToken)
        {
            var paging = request.Normalize(paginationOptions.Value);

            var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
            var normalizedSortBy = request.SortBy?.ToLowerInvariant();

            IQueryable<PublisherProjection> query = session.Query<PublisherProjection>();

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

            var dtos = pagedList.ToList().Select(x => new PublisherDto(
                x.Id,
                x.Name,
                ETagHelper.GenerateETag(x.Version)
            )).ToList();

            return Results.Ok(new PagedListDto<PublisherDto>(dtos, pagedList.PageNumber, pagedList.PageSize, pagedList.TotalItemCount));
        }

        static Task<IResult> CreatePublisher(
            [FromBody] Commands.CreatePublisherRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            CancellationToken cancellationToken)
        {
            var command = new Commands.CreatePublisher(request.Name);
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> UpdatePublisher(
            Guid id,
            [FromBody] Commands.UpdatePublisherRequest request,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.UpdatePublisher(id, request.Name) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> SoftDeletePublisher(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.SoftDeletePublisher(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }

        static Task<IResult> RestorePublisher(
            Guid id,
            [FromServices] IMessageBus bus,
            [FromServices] ITenantContext tenantContext,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var etag = context.Request.Headers["If-Match"].FirstOrDefault();
            var command = new Commands.RestorePublisher(id) { ETag = etag };
            return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
        }
    }
}
