using BookStore.Web.Services;

namespace BookStore.Web.Infrastructure;

public class TenantHeaderHandler(TenantService tenantService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("X-Tenant-ID"))
        {
            request.Headers.Add("X-Tenant-ID", tenantService.CurrentTenantId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
