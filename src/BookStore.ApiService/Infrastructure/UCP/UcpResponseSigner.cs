using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpResponseSigner(IOptions<UcpKeyOptions> options)
{
    const string SignatureName = "sig1";

    public bool TrySignCompleteCheckoutResponse(IHeaderDictionary headers, int statusCode, string contentType, ReadOnlySpan<byte> body)
    {
        var keyOptions = options.Value;

        if (string.IsNullOrWhiteSpace(keyOptions.SigningPrivateKeyBase64))
        {
            return false;
        }

        byte[] privateKey;
        try
        {
            privateKey = Convert.FromBase64String(keyOptions.SigningPrivateKeyBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var digest = ComputeContentDigest(body);
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signatureInput = BuildSignatureInput(created, keyOptions.SigningKeyId);
        var signatureBase = BuildSignatureBase(statusCode, contentType, digest, signatureInput);

        byte[] signature;
        try
        {
            signature = HMACSHA256.HashData(privateKey, Encoding.UTF8.GetBytes(signatureBase));
        }
        catch (CryptographicException)
        {
            return false;
        }

        headers["Content-Digest"] = digest;
        headers["Signature-Input"] = $"{SignatureName}={signatureInput}";
        headers["Signature"] = $"{SignatureName}=:{Convert.ToBase64String(signature)}:";

        return true;
    }

    static string ComputeContentDigest(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[32];
        _ = SHA256.HashData(body, hash);
        return $"sha-256=:{Convert.ToBase64String(hash)}:";
    }

    static string BuildSignatureInput(long created, string keyId)
        => $"(\"@status\" \"content-digest\" \"content-type\");created={created.ToString(CultureInfo.InvariantCulture)};keyid=\"{keyId}\"";

    static string BuildSignatureBase(int statusCode, string contentType, string contentDigest, string signatureInput)
        => string.Create(CultureInfo.InvariantCulture, $"\"@status\": {statusCode}\n\"content-digest\": {contentDigest}\n\"content-type\": {contentType}\n\"@signature-params\": {signatureInput}");
}
