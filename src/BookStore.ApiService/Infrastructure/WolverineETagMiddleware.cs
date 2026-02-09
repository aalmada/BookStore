using BookStore.Shared.Commands;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Wolverine middleware to propagate ETag from HTTP header to command
/// </summary>
public static class WolverineETagMiddleware
{
    public static void Before(IMessageContext context, IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var message = context.Envelope?.Message;
        if (message is IHaveETag command)
        {
            var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ifMatch))
            {
                command.ETag = ifMatch;
            }
        }
    }
}
