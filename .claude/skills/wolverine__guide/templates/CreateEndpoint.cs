using BookStore.ApiService.Infrastructure.Tenant;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Endpoints.Admin;

// Thin routing layer — place in Endpoints/Admin/Admin{Resource}Endpoints.cs
public static class Admin{Resource}Endpoints
{
    public static RouteGroupBuilder MapAdmin{Resource}Endpoints(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/", Create{Resource})
            .WithName("Create{Resource}")
            .WithSummary("Create a new {resource}");

        return group.RequireAuthorization("Admin");
    }

    static Task<IResult> Create{Resource}(
        [FromBody] Create{Resource}Request request,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var command = new Commands.Create{Resource}(request.Name /*, other args */);
        return bus.InvokeAsync<IResult>(command, new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);
    }
}
