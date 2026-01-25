namespace BookStore.ApiService.Infrastructure;

public static class DeviceNameParser
{
    /// <summary>
    /// Parses the User-Agent string to extract a human-readable device/browser name.
    /// </summary>
    public static string Parse(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return "Unknown Device";
        }

        // Simple heuristics for common browsers/OS
        // Order matters: check more specific first

        var ua = userAgent;

        // Detect OS
        var os = "Unknown OS";
        if (ua.Contains("Windows"))
        {
            os = "Windows";
        }
        else if (ua.Contains("iPhone") || ua.Contains("iPad") || ua.Contains("iPod"))
        {
            os = "iOS";
        }
        else if (ua.Contains("Macintosh") || ua.Contains("Mac OS X"))
        {
            os = "macOS";
        }
        else if (ua.Contains("Android"))
        {
            os = "Android";
        }
        else if (ua.Contains("Linux"))
        {
            os = "Linux";
        }

        // Detect Browser
        var browser = "Unknown Browser";
        if (ua.Contains("Edg/"))
        {
            browser = "Edge";
        }
        else if (ua.Contains("Chrome") && !ua.Contains("Edg/"))
        {
            browser = "Chrome"; // Chrome user agent also contains Safari
        }
        else if (ua.Contains("Firefox"))
        {
            browser = "Firefox";
        }
        else if (ua.Contains("Safari") && !ua.Contains("Chrome"))
        {
            browser = "Safari";
        }

        // Passkey managers/native apps often don't send standard browser UAs, or send specific ones.
        // If it looks like a browser, combine them.
        if (browser != "Unknown Browser" && os != "Unknown OS")
        {
            return $"{browser} on {os}";
        }
        else if (browser != "Unknown Browser")
        {
            return browser;
        }
        else if (os != "Unknown OS")
        {
            return os;
        }

        // Fallback: truncate UA if too long
        return ua.Length > 30 ? ua[..27] + "..." : ua;
    }
}
