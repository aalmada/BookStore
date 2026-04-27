using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpProfileService(IOptions<UcpProfileOptions> options, IOptions<UcpKeyOptions> keyOptions)
{
    const string UcpVersion = "2026-04-08";

    public object BuildProfile()
    {
        var opts = options.Value;
        var keys = keyOptions.Value;
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

        if (keys.RequireSignatures)
        {
            profile["authentication"] = new object[]
            {
                new
                {
                    type = "http_message_signatures",
                    algorithms = new[] { "Ed25519" },
                    key_id = keys.SigningKeyId
                }
            };
        }

        if (!string.IsNullOrEmpty(keys.SigningPublicKeyBase64))
        {
            profile["keys"] = new object[]
            {
                new
                {
                    id = keys.SigningKeyId,
                    algorithm = "Ed25519",
                    public_key = keys.SigningPublicKeyBase64
                }
            };
        }

        return profile;
    }
}
