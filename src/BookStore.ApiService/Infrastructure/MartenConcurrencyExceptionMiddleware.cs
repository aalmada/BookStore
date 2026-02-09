using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Middleware to catch Marten ConcurrencyException and return 412 Precondition Failed
/// </summary>
public class MartenConcurrencyExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MartenConcurrencyExceptionMiddleware> _logger;

    public MartenConcurrencyExceptionMiddleware(RequestDelegate next, ILogger<MartenConcurrencyExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var unwrapped = UnwrapException(ex);
            
            if (unwrapped.GetType().Name == "ConcurrencyException" || 
                unwrapped.GetType().Name == "EventStreamUnexpectedMaxEventIdException" ||
                unwrapped.GetType().Name == "ExistingStreamIdCollisionException" ||
                unwrapped.GetType().Name.Contains("EventStreamUnexpectedMaxEventIdException") ||
                IsPostgresConcurrencyException(unwrapped))
            {
#pragma warning disable CA1848
                _logger.LogError(unwrapped, "Concurrency conflict detected in Marten. Message: {Message}", unwrapped.Message);
#pragma warning restore CA1848

                try
                {
                    System.IO.File.AppendAllText("marten_concurrency_errors.log", $"[{DateTimeOffset.UtcNow}] TraceId={context.TraceIdentifier} {unwrapped.GetType().Name}: {unwrapped.Message}{Environment.NewLine}{unwrapped.StackTrace}{Environment.NewLine}");
                }
                catch { /* Ignore logging errors */ }

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Title = "Precondition Failed",
                        Status = 412,
                        Detail = $"Concurrency conflict: {unwrapped.Message}. The resource has been modified since you last retrieved it. Please refresh and try again."
                    });
                }
                return;
            }

            // Re-throw if not handled
            throw;
        }
    }

    private static Exception UnwrapException(Exception ex)
    {
        var current = ex;
        while (current.InnerException != null && 
               (current is AggregateException || 
                current.GetType().Name.Contains("Invocation") || 
                current.GetType().Name.Contains("Wolverine") ||
                current.GetType().Name.Contains("TargetInvocation")))
        {
            current = current.InnerException;
        }
        return current;
    }

    private static bool IsPostgresConcurrencyException(Exception ex)
    {
        if (ex is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            var target = pgEx.ConstraintName ?? pgEx.Message;
            return target.Contains("mt_events", StringComparison.OrdinalIgnoreCase) || 
                   target.Contains("mt_streams", StringComparison.OrdinalIgnoreCase);
        }

        if (ex is Marten.Exceptions.MartenCommandException martenEx && 
            martenEx.InnerException is Npgsql.PostgresException innerPgEx && 
            innerPgEx.SqlState == "23505")
        {
            var target = innerPgEx.ConstraintName ?? innerPgEx.Message;
            return target.Contains("mt_events", StringComparison.OrdinalIgnoreCase) || 
                   target.Contains("mt_streams", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

public static class MartenConcurrencyExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseMartenConcurrencyException(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MartenConcurrencyExceptionMiddleware>();
    }
}
