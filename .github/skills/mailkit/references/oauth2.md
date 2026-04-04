# OAuth2 Authentication

MailKit uses SASL mechanisms for authentication. For OAuth2, use `SaslMechanismOAuth2` (XOAUTH2) or `SaslMechanismOAuthBearer` (OAUTHBEARER). Pass the mechanism to `AuthenticateAsync` instead of username/password.

---

## Generic OAuth2 (any provider)

```csharp
using MailKit.Security;

var oauth2 = new SaslMechanismOAuth2(username, accessToken);

using var client = new ImapClient();
await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, ct);
await client.AuthenticateAsync(oauth2, ct);

// Same pattern for SmtpClient and Pop3Client
using var smtp = new SmtpClient();
await smtp.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, ct);
await smtp.AuthenticateAsync(oauth2, ct);
```

---

## Gmail with Google.Apis.Auth

Add packages: `MailKit`, `Google.Apis.Auth`

```csharp
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using MailKit.Security;

const string GmailAccount = "user@gmail.com";

var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
{
    ClientSecrets = new ClientSecrets
    {
        ClientId = "YOUR_CLIENT_ID.apps.googleusercontent.com",
        ClientSecret = "YOUR_CLIENT_SECRET"
    },
    Scopes = ["https://mail.google.com/"],
    DataStore = new FileDataStore("CredentialCache", false),
    LoginHint = GmailAccount
});

var codeReceiver = new LocalServerCodeReceiver();
var authCode = new AuthorizationCodeInstalledApp(flow, codeReceiver);
var credential = await authCode.AuthorizeAsync(GmailAccount, CancellationToken.None);

if (credential.Token.IsStale)
    await credential.RefreshTokenAsync(CancellationToken.None);

var oauth2 = new SaslMechanismOAuthBearer(credential.UserId, credential.Token.AccessToken);

using var client = new ImapClient();
await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect, ct);
await client.AuthenticateAsync(oauth2, ct);
```

### Gmail in ASP.NET Core (server-side token)

```csharp
// In a controller/action decorated with [GoogleScopedAuthorize]
[GoogleScopedAuthorize("https://mail.google.com/")]
public async Task<IActionResult> SendAsync([FromServices] IGoogleAuthProvider auth)
{
    var cred  = await auth.GetCredentialAsync();
    var token = await cred.UnderlyingCredential.GetAccessTokenForRequestAsync();
    var oauth2 = new SaslMechanismOAuthBearer("user@gmail.com", token);

    using var client = new SmtpClient();
    await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
    await client.AuthenticateAsync(oauth2);
    await client.SendAsync(message);
    await client.DisconnectAsync(true);
    return Ok();
}
```

---

## Exchange / Office 365 with MSAL

Add packages: `MailKit`, `Microsoft.Identity.Client`

```csharp
using Microsoft.Identity.Client;
using MailKit.Security;

// Confidential client (service/daemon) — no user interaction
var app = ConfidentialClientApplicationBuilder
    .Create(clientId)
    .WithClientSecret(clientSecret)
    .WithTenantId(tenantId)
    .Build();

var result = await app
    .AcquireTokenForClient(["https://outlook.office365.com/.default"])
    .ExecuteAsync();

var oauth2 = new SaslMechanismOAuth2("username@outlook.com", result.AccessToken);

using var client = new ImapClient();
await client.ConnectAsync("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect);
await client.AuthenticateAsync(oauth2);
```

```csharp
// Public client (desktop app) — interactive user login
var app = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
    .WithRedirectUri("http://localhost")
    .Build();

var result = await app
    .AcquireTokenInteractive(["https://outlook.office365.com/IMAP.AccessAsUser.All"])
    .WithLoginHint("user@outlook.com")
    .ExecuteAsync();

// Always use result.Account.Username (user may select a different account)
var oauth2 = new SaslMechanismOAuth2(result.Account.Username, result.AccessToken);
```

---

## Token refresh

Access tokens expire (typically in 1 hour). Refresh before each connection, or catch `AuthenticationException` and refresh then reconnect.

```csharp
// Google
if (credential.Token.IsStale)
    await credential.RefreshTokenAsync(ct);
var token = credential.Token.AccessToken;

// MSAL — acquireToken* methods handle cache automatically; expired tokens are refreshed silently
var result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
```
