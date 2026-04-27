using BookStore.ApiService.Infrastructure.UCP;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Infrastructure.UCP;

public class UcpResponseSignerTests
{
    [Test]
    [Category("Unit")]
    public async Task TrySignCompleteCheckoutResponse_WhenPrivateKeyIsMissing_ShouldReturnFalse()
    {
        var options = Options.Create(new UcpKeyOptions
        {
            SigningKeyId = "merchant-key-2026",
            SigningPrivateKeyBase64 = null
        });
        var signer = new UcpResponseSigner(options);
        var headers = new HeaderDictionary();

        var signed = signer.TrySignCompleteCheckoutResponse(
            headers,
            StatusCodes.Status200OK,
            "application/json",
            "{\"status\":\"completed\"}"u8.ToArray());

        _ = await Assert.That(signed).IsFalse();
        _ = await Assert.That(headers.ContainsKey("Signature")).IsFalse();
        _ = await Assert.That(headers.ContainsKey("Signature-Input")).IsFalse();
        _ = await Assert.That(headers.ContainsKey("Content-Digest")).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task TrySignCompleteCheckoutResponse_WhenPrivateKeyIsInvalidBase64_ShouldReturnFalse()
    {
        var options = Options.Create(new UcpKeyOptions
        {
            SigningKeyId = "merchant-key-2026",
            SigningPrivateKeyBase64 = "not-base64"
        });
        var signer = new UcpResponseSigner(options);
        var headers = new HeaderDictionary();

        var signed = signer.TrySignCompleteCheckoutResponse(
            headers,
            StatusCodes.Status200OK,
            "application/json",
            "{\"status\":\"completed\"}"u8.ToArray());

        _ = await Assert.That(signed).IsFalse();
        _ = await Assert.That(headers.ContainsKey("Signature")).IsFalse();
    }
}
