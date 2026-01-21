using BookStore.Client.Services;
using BookStore.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BookStore.Web.Infrastructure;

public class LoggingHubFilter : IHubFilter
{
    readonly ILogger<LoggingHubFilter> _logger;

    public LoggingHubFilter(ILogger<LoggingHubFilter> logger) => _logger = logger;

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Blazor Server circuits share a scope. We can resolve scoped services from the helper.
        // The HubInvocationContext.ServiceProvider should be the scoped provider for the circuit.
        var serviceProvider = invocationContext.ServiceProvider;

        var correlationService = serviceProvider.GetService<ClientContextService>();
        var tenantService = serviceProvider.GetService<TenantService>();

        if (correlationService == null || tenantService == null)
        {
            return await next(invocationContext);
        }

        var scopeState = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationService.CorrelationId,
            ["CausationId"] = correlationService.CausationId,
            ["TenantId"] = tenantService.CurrentTenantId
        };

        var browserInfo = correlationService.Browser;
        if (browserInfo != null)
        {
            scopeState["Browser.UserAgent"] = browserInfo.UserAgent;
            scopeState["Browser.Screen"] = browserInfo.Screen;
            scopeState["Browser.Language"] = browserInfo.Language;
            scopeState["Browser.Timezone"] = browserInfo.Timezone;
        }

        try
        {
            using (_logger.BeginScope(scopeState))
            {
                return await next(invocationContext);
            }
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848
            _logger.LogError(ex, "Error executing hub method: {MethodName}", invocationContext.HubMethodName);
#pragma warning restore CA1848
            throw;
        }
    }
}
