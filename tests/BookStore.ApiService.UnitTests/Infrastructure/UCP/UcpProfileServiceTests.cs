using System.Text.Json;
using BookStore.ApiService.Infrastructure.UCP;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Infrastructure.UCP;

public class UcpProfileServiceTests
{
    [Test]
    [Category("Unit")]
    public async Task BuildProfile_WhenSignaturesRequired_ShouldAdvertiseAuthentication()
    {
        var profileOptions = Options.Create(new UcpProfileOptions
        {
            ServiceBaseUrl = "https://merchant.example"
        });
        var keyOptions = Options.Create(new UcpKeyOptions
        {
            RequireSignatures = true,
            SigningKeyId = "merchant-key-2026"
        });

        var service = new UcpProfileService(profileOptions, keyOptions);
        var profileJson = JsonSerializer.SerializeToElement(service.BuildProfile());

        var authentication = profileJson.GetProperty("authentication").EnumerateArray().First();
        _ = await Assert.That(authentication.GetProperty("type").GetString()).IsEqualTo("http_message_signatures");
        _ = await Assert.That(authentication.GetProperty("key_id").GetString()).IsEqualTo("merchant-key-2026");
    }

    [Test]
    [Category("Unit")]
    public async Task BuildProfile_WhenPublicKeyIsConfigured_ShouldIncludeKeyMetadata()
    {
        var profileOptions = Options.Create(new UcpProfileOptions
        {
            ServiceBaseUrl = "https://merchant.example"
        });
        var keyOptions = Options.Create(new UcpKeyOptions
        {
            SigningKeyId = "merchant-key-2026",
            SigningPublicKeyBase64 = "dGVzdC1wdWJsaWMta2V5"
        });

        var service = new UcpProfileService(profileOptions, keyOptions);
        var profileJson = JsonSerializer.SerializeToElement(service.BuildProfile());

        var key = profileJson.GetProperty("keys").EnumerateArray().First();
        _ = await Assert.That(key.GetProperty("id").GetString()).IsEqualTo("merchant-key-2026");
        _ = await Assert.That(key.GetProperty("algorithm").GetString()).IsEqualTo("Ed25519");
        _ = await Assert.That(key.GetProperty("public_key").GetString()).IsEqualTo("dGVzdC1wdWJsaWMta2V5");
    }
}
