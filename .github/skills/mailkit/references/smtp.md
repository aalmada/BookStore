# SMTP — Sending Email

## Basic send (async)

```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

using var client = new SmtpClient();
await client.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, cancellationToken);
await client.AuthenticateAsync("username", "password", cancellationToken);
await client.SendAsync(message, cancellationToken);   // message is a MimeMessage
await client.DisconnectAsync(true, cancellationToken);
```

---

## `SecureSocketOptions`

| Value | Port | Use case |
|-------|------|---------|
| `None` | 25 | Plain text — dev/local only |
| `Auto` | any | Auto-detects based on port |
| `SslOnConnect` | 465 | SMTPS — TLS from the start |
| `StartTls` | 587 | Upgrade to TLS after connect (recommended) |
| `StartTlsWhenAvailable` | 587 | Upgrade if server supports it |

---

## DI-friendly service pattern

```csharp
// Registration (Program.cs)
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailSender, MailKitEmailSender>();

// Options class
public class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

// Implementation
public sealed class MailKitEmailSender(IOptions<SmtpSettings> opts) : IEmailSender
{
    private readonly SmtpSettings _s = opts.Value;

    public async Task SendAsync(MimeMessage message, CancellationToken ct = default)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(_s.Host, _s.Port, _s.Security, ct);

        if (!string.IsNullOrEmpty(_s.Username))
            await client.AuthenticateAsync(_s.Username, _s.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
```

---

## Error handling

```csharp
try
{
    await client.ConnectAsync(host, port, options, ct);
    await client.AuthenticateAsync(user, pass, ct);
    await client.SendAsync(message, ct);
}
catch (SmtpCommandException ex)
{
    // SMTP-level error: mailbox not found, relay denied, etc.
    // ex.StatusCode is an SmtpStatusCode enum value
    logger.LogError("SMTP command error {StatusCode}: {Message}", ex.StatusCode, ex.Message);
}
catch (SmtpProtocolException ex)
{
    // Low-level protocol failure (connection lost, bad server response)
    logger.LogError("SMTP protocol error: {Message}", ex.Message);
}
catch (AuthenticationException ex)
{
    logger.LogError("SMTP authentication failed: {Message}", ex.Message);
}
finally
{
    if (client.IsConnected)
        await client.DisconnectAsync(true);
}
```

---

## Sending to multiple recipients

Batch sends to a large list: use a single connected client and call `SendAsync` for each message rather than creating a new client per message.

```csharp
await client.ConnectAsync(host, port, options, ct);
await client.AuthenticateAsync(user, pass, ct);

foreach (var message in messages)
    await client.SendAsync(message, ct);

await client.DisconnectAsync(true, ct);
```
