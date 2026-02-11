using BookStore.Client.Logging;
using Microsoft.Extensions.Logging;

namespace BookStore.Client.Infrastructure;

/// <summary>
/// A DelegatingHandler that logs errors when the API returns a non-success status code.
/// </summary>
public class BookStoreErrorHandler(ILogger<BookStoreErrorHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.ApiError(logger, request.Method, request.RequestUri, response.StatusCode, content);
        }

        return response;
    }
}
