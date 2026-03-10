using BookStore.ApiService.Infrastructure.Tenant;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

// Method snippet to add inside Admin{Resource}Endpoints.MapAdmin{Resource}Endpoints()
// group.MapPut("/{id:guid}", Update{Resource})
//     .WithName("Update{Resource}")
//     .WithSummary("Update a {resource}");

// Inside the Admin{Resource}Endpoints static class:
static Task<IResult> Update{Resource}(
    Guid id,
    [FromBody] Update{Resource}Request request,
    [FromServices] IMessageBus bus,
    [FromServices] ITenantContext tenantContext,
    HttpContext context,
    CancellationToken cancellationToken)
{
    var etag = context.Request.Headers["If-Match"].FirstOrDefault();
    var command = new Commands.Update{Resource}(id, request.Name /*, other args */) { ETag = etag };
    return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
}
