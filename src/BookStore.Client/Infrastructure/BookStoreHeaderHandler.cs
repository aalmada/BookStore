using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;

namespace BookStore.Client.Infrastructure;

/// <summary>
/// A DelegatingHandler that adds common headers to outgoing requests.
/// </summary>
public class BookStoreHeaderHandler : DelegatingHandler
{
    const string ApiVersionHeader = "api-version";
    const string CorrelationIdHeader = "X-Correlation-ID";
    const string CausationIdHeader = "X-Causation-ID";
    const string DefaultApiVersion = "1.0";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add api-version if missing
        if (!request.Headers.Contains(ApiVersionHeader))
        {
            request.Headers.Add(ApiVersionHeader, DefaultApiVersion);
        }

        // Add Accept-Language if missing
        if (!request.Headers.Contains("Accept-Language"))
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (!string.IsNullOrEmpty(culture))
            {
                request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
            }
        }

        // Add Correlation IDs if missing and activity is present
        var activity = Activity.Current;
        if (activity != null)
        {
            if (!request.Headers.Contains(CorrelationIdHeader) && activity.TraceId != default)
            {
                request.Headers.Add(CorrelationIdHeader, activity.TraceId.ToString());
            }

            if (!request.Headers.Contains(CausationIdHeader) && activity.ParentId != null)
            {
                request.Headers.Add(CausationIdHeader, activity.ParentId);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
