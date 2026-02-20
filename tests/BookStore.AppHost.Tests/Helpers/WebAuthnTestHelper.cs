using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace BookStore.AppHost.Tests.Helpers;

/// <summary>
/// Provides end-to-end WebAuthn (passkey) flows for integration tests using a
/// headless Chromium browser with a Playwright virtual authenticator.
///
/// The virtual authenticator satisfies the ASP.NET Core Identity passkey
/// cryptographic verification (real CBOR / COSE key pair is generated), so
/// the test exercises the full attestation / assertion code paths in
/// PasskeyEndpoints.cs without requiring a physical security key.
///
/// Usage:
///   await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
///   var credential = await webAuthn.RegisterPasskeyAsync(email, tenantId);
///   var loginResponse = await webAuthn.LoginWithPasskeyAsync(credential, tenantId);
/// </summary>
public sealed class WebAuthnTestHelper : IAsyncDisposable
{
    readonly IPlaywright _playwright;
    readonly IBrowser _browser;
    readonly IBrowserContext _context;
    readonly IPage _page;
    readonly string _apiBaseUrl;

    WebAuthnTestHelper(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        string apiBaseUrl)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        _page = page;
        _apiBaseUrl = apiBaseUrl;
    }

    /// <summary>
    /// Creates a configured helper with a virtual authenticator attached to a
    /// headless Chromium browser that is pointed at the API origin.
    /// </summary>
    public static async Task<WebAuthnTestHelper> CreateAsync()
    {
        var app = GlobalHooks.App
            ?? throw new InvalidOperationException("GlobalHooks.App must be initialized before using WebAuthnTestHelper.");

        // Resolve the base URL of the running API service
        var httpClient = app.CreateHttpClient("apiservice");
        var baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            // Must match the rpId ("localhost") configured in IdentityPasskeyOptions
            BaseURL = baseUrl,
            IgnoreHTTPSErrors = true,
        });

        // Enable WebAuthn virtual authenticator support via CDP
        await context.AddCookiesAsync([]);
        var page = await context.NewPageAsync();

        // Navigate to the API origin so navigator.credentials API is available
        // under the correct origin (http://localhost:<port>).
        // A 404 or JSON response is fine — we only need the origin to be set.
        _ = await page.GotoAsync(baseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 10_000
        });

        // Add a virtual authenticator via CDP: CTAP2, internal (platform), user-verifying.
        // IVirtualAuthenticator does not exist in Playwright .NET — CDP is the correct approach.
        var cdp = await context.NewCDPSessionAsync(page);
        _ = await cdp.SendAsync("WebAuthn.enable", new Dictionary<string, object> { ["enableUI"] = false });
        _ = await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new Dictionary<string, object>
            {
                ["protocol"] = "ctap2",
                ["transport"] = "internal",
                ["hasResidentKey"] = true,
                ["hasUserVerification"] = true,
                ["isUserVerified"] = true,
                ["automaticPresenceSimulation"] = true,
            }
        });

        return new WebAuthnTestHelper(playwright, browser, context, page, baseUrl);
    }

    /// <summary>
    /// Performs a full passkey registration flow via the API.
    /// Returns the credential info needed for subsequent login calls.
    /// </summary>
    public async Task<RegisteredPasskey> RegisterPasskeyAsync(
        string email,
        string tenantId,
        string? accessToken = null)
    {
        var httpClient = BuildHttpClient(tenantId, accessToken);

        // Step 1 — get attestation options from the API
        var optionsResponse = await httpClient.PostAsJsonAsync("/account/attestation/options", new { email });
        _ = optionsResponse.EnsureSuccessStatusCode();
        var optionsJson = await optionsResponse.Content.ReadAsStringAsync();

        // optionsJson shape: { options: { ... }, userId: "..." }
        var envelope = JsonDocument.Parse(optionsJson);
        var optionsElement = envelope.RootElement.GetProperty("options");
        var userId = envelope.RootElement.GetProperty("userId").GetString()!;
        var challengeJson = optionsElement.GetRawText();

        // Step 2 — call navigator.credentials.create() inside the browser
        var credentialJson = await _page.EvaluateAsync<string>(
            @"async (challengeJson) => {
                const options = JSON.parse(challengeJson);

                // Convert base64url fields to ArrayBuffers
                function b64urlToBuffer(b64url) {
                    const b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
                    const bin = atob(b64);
                    const arr = new Uint8Array(bin.length);
                    for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
                    return arr.buffer;
                }
                function bufferToB64url(buf) {
                    const arr = new Uint8Array(buf);
                    let str = '';
                    for (let i = 0; i < arr.length; i++) str += String.fromCharCode(arr[i]);
                    return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
                }

                options.challenge = b64urlToBuffer(options.challenge);
                options.user.id = b64urlToBuffer(options.user.id);
                if (options.excludeCredentials) {
                    options.excludeCredentials = options.excludeCredentials.map(c => ({
                        ...c,
                        id: b64urlToBuffer(c.id)
                    }));
                }

                const cred = await navigator.credentials.create({ publicKey: options });

                return JSON.stringify({
                    id: cred.id,
                    rawId: bufferToB64url(cred.rawId),
                    type: cred.type,
                    response: {
                        clientDataJSON: bufferToB64url(cred.response.clientDataJSON),
                        attestationObject: bufferToB64url(cred.response.attestationObject),
                    },
                    clientExtensionResults: cred.getClientExtensionResults(),
                });
            }",
            challengeJson);

        // Step 3 — submit attestation result to the API
        var resultResponse = await httpClient.PostAsJsonAsync("/account/attestation/result", new
        {
            credentialJson,
            email,
            userId
        });

        if (!resultResponse.IsSuccessStatusCode)
        {
            var errorBody = await resultResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"POST /account/attestation/result failed with {(int)resultResponse.StatusCode} {resultResponse.ReasonPhrase}. Body: {errorBody}",
                null,
                resultResponse.StatusCode);
        }

        // Extract credential ID so we can use it in login flows
        var credDoc = JsonDocument.Parse(credentialJson);
        var rawId = credDoc.RootElement.GetProperty("rawId").GetString()!;

        return new RegisteredPasskey(rawId, email, userId, tenantId);
    }

    /// <summary>
    /// Creates a real WebAuthn attestation credential via the browser's virtual authenticator
    /// but does NOT post the result to the API.
    /// Use this to get a valid <paramref name="credentialJson"/> / <paramref name="userId"/>
    /// for concurrent-registration race-condition tests.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="HttpClient"/> carries the attestation-state cookie that must
    /// be included in the follow-up POST to <c>/account/attestation/result</c>.
    /// All concurrent requests should use this same client so they share the cookie.
    /// </remarks>
    public async Task<(string CredentialJson, string Email, string UserId, HttpClient Client)> CreateAttestationCredentialAsync(
        string email,
        string tenantId)
    {
        var httpClient = BuildHttpClient(tenantId);

        var optionsResponse = await httpClient.PostAsJsonAsync("/account/attestation/options", new { email });
        _ = optionsResponse.EnsureSuccessStatusCode();
        var optionsJson = await optionsResponse.Content.ReadAsStringAsync();

        var envelope = JsonDocument.Parse(optionsJson);
        var optionsElement = envelope.RootElement.GetProperty("options");
        var userId = envelope.RootElement.GetProperty("userId").GetString()!;
        var challengeJson = optionsElement.GetRawText();

        var credentialJson = await _page.EvaluateAsync<string>(
            @"async (challengeJson) => {
                const options = JSON.parse(challengeJson);

                function b64urlToBuffer(b64url) {
                    const b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
                    const bin = atob(b64);
                    const arr = new Uint8Array(bin.length);
                    for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
                    return arr.buffer;
                }
                function bufferToB64url(buf) {
                    const arr = new Uint8Array(buf);
                    let str = '';
                    for (let i = 0; i < arr.length; i++) str += String.fromCharCode(arr[i]);
                    return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
                }

                options.challenge = b64urlToBuffer(options.challenge);
                options.user.id = b64urlToBuffer(options.user.id);
                if (options.excludeCredentials) {
                    options.excludeCredentials = options.excludeCredentials.map(c => ({
                        ...c,
                        id: b64urlToBuffer(c.id)
                    }));
                }

                const cred = await navigator.credentials.create({ publicKey: options });

                return JSON.stringify({
                    id: cred.id,
                    rawId: bufferToB64url(cred.rawId),
                    type: cred.type,
                    response: {
                        clientDataJSON: bufferToB64url(cred.response.clientDataJSON),
                        attestationObject: bufferToB64url(cred.response.attestationObject),
                    },
                    clientExtensionResults: cred.getClientExtensionResults(),
                });
            }",
            challengeJson);

        return (credentialJson, email, userId, httpClient);
    }

    /// <summary>
    /// Performs a full passkey login (assertion) flow via the API.
    /// Returns the login response containing access and refresh tokens.
    /// </summary>
    public async Task<LoginResult> LoginWithPasskeyAsync(RegisteredPasskey passkey)
    {
        var httpClient = BuildHttpClient(passkey.TenantId);

        // Step 1 — get assertion options
        var optionsResponse = await httpClient.PostAsJsonAsync("/account/assertion/options", new { email = passkey.Email });
        _ = optionsResponse.EnsureSuccessStatusCode();
        var assertionOptionsJson = await optionsResponse.Content.ReadAsStringAsync();

        // Step 2 — call navigator.credentials.get() in the browser
        var credentialJson = await _page.EvaluateAsync<string>(
            @"async (assertionOptionsJson) => {
                const options = JSON.parse(assertionOptionsJson);

                function b64urlToBuffer(b64url) {
                    const b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
                    const bin = atob(b64);
                    const arr = new Uint8Array(bin.length);
                    for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
                    return arr.buffer;
                }
                function bufferToB64url(buf) {
                    const arr = new Uint8Array(buf);
                    let str = '';
                    for (let i = 0; i < arr.length; i++) str += String.fromCharCode(arr[i]);
                    return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
                }

                options.challenge = b64urlToBuffer(options.challenge);
                if (options.allowCredentials) {
                    options.allowCredentials = options.allowCredentials.map(c => ({
                        ...c,
                        id: b64urlToBuffer(c.id)
                    }));
                }

                const cred = await navigator.credentials.get({ publicKey: options });

                return JSON.stringify({
                    id: cred.id,
                    rawId: bufferToB64url(cred.rawId),
                    type: cred.type,
                    response: {
                        clientDataJSON: bufferToB64url(cred.response.clientDataJSON),
                        authenticatorData: bufferToB64url(cred.response.authenticatorData),
                        signature: bufferToB64url(cred.response.signature),
                        userHandle: cred.response.userHandle ? bufferToB64url(cred.response.userHandle) : null,
                    },
                    clientExtensionResults: cred.getClientExtensionResults(),
                });
            }",
            assertionOptionsJson);

        // Step 3 — submit assertion result
        var resultResponse = await httpClient.PostAsJsonAsync("/account/assertion/result", new { credentialJson });

        if (!resultResponse.IsSuccessStatusCode)
        {
            var errorBody = await resultResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"POST /account/assertion/result failed with {(int)resultResponse.StatusCode} {resultResponse.ReasonPhrase}. Body: {errorBody}",
                null,
                resultResponse.StatusCode);
        }

        var result = await resultResponse.Content.ReadFromJsonAsync<LoginResult>()
            ?? throw new InvalidOperationException("Passkey assertion result was null.");

        return result;
    }

    /// <summary>
    /// Creates a fresh <see cref="HttpClient"/> with its own isolated <see cref="System.Net.CookieContainer"/>
    /// pointed at the API service base URL.
    ///
    /// Using a dedicated <see cref="HttpClientHandler"/> (instead of one from <see cref="IHttpClientFactory"/>)
    /// ensures that the passkey state cookie (<c>.AspNetCore.Identity.TwoFactorUserId</c>) set by
    /// <c>MakePasskeyCreationOptionsAsync</c> / <c>MakePasskeyRequestOptionsAsync</c> cannot leak into
    /// or be overwritten by cookies from other tests running in the same process.
    /// </summary>
    HttpClient BuildHttpClient(string tenantId, string? accessToken = null)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer(),
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_apiBaseUrl.TrimEnd('/') + "/"),
        };
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        if (!string.IsNullOrEmpty(accessToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.DisposeAsync();
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

/// <summary>Represents a passkey credential that has been successfully registered.</summary>
public record RegisteredPasskey(string RawId, string Email, string UserId, string TenantId);

/// <summary>JWT token response from a successful assertion.</summary>
public record LoginResult(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);
