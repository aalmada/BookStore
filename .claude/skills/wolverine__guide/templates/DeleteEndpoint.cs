using BookStore.ApiService.Infrastructure.Tenant;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

// Method snippet to add inside Admin{Resource}Endpoints.MapAdmin{Resource}Endpoints()
// group.MapDelete("/{id:guid}", Delete{Resource})
//     .WithName("Delete{Resource}")
//     .WithSummary("Delete a {resource}");

// Inside the Admin{Resource}Endpoints static class:
static Task<IResult> Delete{Resource}(
    Guid id,
    [FromServices] IMessageBus bus,
    [FromServices] ITenantContext tenantContext,
    HttpContext context,
    CancellationToken cancellationToken)
{
    var etag = context.Request.Headers["If-Match"].FirstOrDefault();
    var command = new Commands.SoftDelete{Resource}(id) { ETag = etag };
    return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
}
