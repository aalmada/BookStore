using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpProfileService(IOptions<UcpProfileOptions> options)
{
    const string UcpVersion = "2026-04-08";

    public object BuildProfile()
    {
        var opts = options.Value;
        var baseUrl = opts.ServiceBaseUrl.TrimEnd('/');

        var capabilities = new Dictionary<string, object[]>
        {
            ["dev.ucp.shopping.checkout"] =
            [
                new { version = UcpVersion, extends = Array.Empty<object>() }
            ],
            ["dev.ucp.shopping.catalog"] =
            [
                new { version = UcpVersion, extends = Array.Empty<object>() }
            ],
            ["dev.ucp.shopping.fulfillment"] =
            [
                new { version = UcpVersion, extends = new[] { new { name = "dev.ucp.shopping.checkout" } } }
            ]
        };

        var services = new Dictionary<string, object[]>
        {
            ["dev.ucp.shopping.checkout"] =
            [
                new
                {
                    id = "checkout_rest",
                    transport = "rest",
                    rest = new { endpoint = $"{baseUrl}/api/ucp" }
                }
            ],
            ["dev.ucp.shopping.catalog"] =
            [
                new
                {
                    id = "catalog_rest",
                    transport = "rest",
                    rest = new { endpoint = $"{baseUrl}/api/ucp" }
                }
            ]
        };

        var paymentHandlers = new Dictionary<string, object[]>
        {
            ["dev.bookstore.payment.simulated"] =
            [
                new
                {
                    id = "simulated_payment",
                    version = UcpVersion,
                    config = new { environment = "TEST" }
                }
            ]
        };

        var profile = new Dictionary<string, object>
        {
            ["ucp"] = new { version = UcpVersion },
            ["capabilities"] = capabilities,
            ["services"] = services,
            ["payment_handlers"] = paymentHandlers
        };

        if (!string.IsNullOrEmpty(opts.SigningPublicKeyBase64))
        {
            profile["keys"] = new object[]
            {
                new
                {
                    id = opts.SigningKeyId,
                    algorithm = "Ed25519",
                    public_key = opts.SigningPublicKeyBase64
                }
            };
        }

        return profile;
    }
}
