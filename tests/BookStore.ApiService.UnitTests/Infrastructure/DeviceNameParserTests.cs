using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class DeviceNameParserTests
{
    [Test]
    public async Task Parse_ChromeOnMac_ReturnsExpectedDeviceName()
    {
        var userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var expected = "Chrome on macOS";
        var result = DeviceNameParser.Parse(userAgent);
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Parse_ChromeOnAndroid_ReturnsExpectedDeviceName()
    {
        var userAgent = "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";
        var expected = "Chrome on Android";
        var result = DeviceNameParser.Parse(userAgent);
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Parse_SafariOnIOS_ReturnsExpectedDeviceName()
    {
        var userAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1";
        var expected = "Safari on iOS";
        var result = DeviceNameParser.Parse(userAgent);
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Parse_FirefoxOnWindows_ReturnsExpectedDeviceName()
    {
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:122.0) Gecko/20100101 Firefox/122.0";
        var expected = "Firefox on Windows";
        var result = DeviceNameParser.Parse(userAgent);
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Parse_EdgeOnWindows_ReturnsExpectedDeviceName()
    {
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0";
        var expected = "Edge on Windows";
        var result = DeviceNameParser.Parse(userAgent);
        _ = await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Parse_WithEmptyString_ReturnsUnknownDevice()
    {
        var result = DeviceNameParser.Parse("");
        _ = await Assert.That(result).IsEqualTo("Unknown Device");
    }

    [Test]
    public async Task Parse_WithNonBrowserUserAgent_ReturnsTruncatedString()
    {
        var result = DeviceNameParser.Parse("MyCustomApp/1.0 (Some long detailed info)");
        // Logic truncates > 30 chars
        _ = await Assert.That(result).StartsWith("MyCustomApp/1.0 (Some long ");
        _ = await Assert.That(result).EndsWith("...");
    }
}
