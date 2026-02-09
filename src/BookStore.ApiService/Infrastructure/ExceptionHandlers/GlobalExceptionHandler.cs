using BookStore.Shared.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace BookStore.ApiService.Infrastructure.ExceptionHandlers;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        BookStore.ApiService.Infrastructure.Logging.Log.Infrastructure.UnhandledException(logger, exception, exception.Message);

        try
        {
            System.IO.File.AppendAllText("global_exception.log", $"[{DateTimeOffset.UtcNow}] {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}");
            if (exception.InnerException != null)
            {
                System.IO.File.AppendAllText("global_exception.log", $"INNER: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}{Environment.NewLine}{exception.InnerException.StackTrace}{Environment.NewLine}");
            }
        }
        catch { }

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            Detail = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        problemDetails.Extensions.Add("error", "ERR_INTERNAL_ERROR");

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
