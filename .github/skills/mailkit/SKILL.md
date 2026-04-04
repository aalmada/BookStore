---
name: mailkit
description: Use MailKit and MimeKit to create, parse, send, and receive email in .NET — covering MimeMessage construction (BodyBuilder, HTML/text bodies, attachments, inline images), parsing .eml files, extracting attachments, headers and addresses (MailboxAddress, reply construction), SmtpClient (SecureSocketOptions, OAuth2, DI patterns), ImapClient (FetchAsync, SearchQuery, StoreFlagsRequest, MoveTo, IDLE push notifications), Pop3Client, and SaslMechanismOAuth2 for Gmail and Exchange. Trigger whenever the user writes, reviews, or asks about email creation, MIME messages, .eml parsing, extracting attachments, sending email, reading inbox, IMAP folder management, SmtpClient, ImapClient, BodyBuilder, MimeMessage, MimePart, MailboxAddress, SaslMechanismOAuth2, or building any email feature in .NET — even if they don't mention MailKit or MimeKit by name. Always prefer this skill over guessing; the MIME tree model, BodyBuilder vs manual multipart construction, FolderAccess modes, FetchAsync vs GetMessageAsync, IDLE threading, and OAuth2 SASL wiring all have non-obvious failure modes.
---

# MailKit + MimeKit

MailKit and MimeKit are the standard .NET libraries for email. MimeKit handles MIME message construction and parsing. MailKit builds on top of it to add SMTP, IMAP, and POP3 transport.

**NuGet package**
- `MailKit` — includes MimeKit as a transitive dependency; one package for everything

## Reference Index

| File | Topics |
|------|--------|
| [creating-messages.md](references/creating-messages.md) | `MimeMessage`, `BodyBuilder`, `TextPart`, `MimePart`, multipart, linked resources, inline images |
| [parsing-messages.md](references/parsing-messages.md) | `MimeMessage.Load`, `TextBody`/`HtmlBody`, `MimeIterator`, extracting and saving attachments |
| [headers-addresses.md](references/headers-addresses.md) | `MailboxAddress`, `GroupAddress`, `InternetAddressList`, custom headers, reply construction |
| [smtp.md](references/smtp.md) | `SmtpClient`, connect/auth/send/disconnect, `SecureSocketOptions`, DI pattern, error handling |
| [imap.md](references/imap.md) | `ImapClient`, folder access, `FetchAsync`, `GetMessageAsync`, `SearchQuery`, flags, move/delete |
| [oauth2.md](references/oauth2.md) | `SaslMechanismOAuth2`, Gmail OAuth, Exchange/Office 365, token refresh |
| [imap-idle.md](references/imap-idle.md) | IMAP IDLE push notifications, `CountChanged`, polling fallback |

## Quick Reference

```csharp
// Build a message
var message = new MimeMessage();
message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
message.Subject = "Hello";

var builder = new BodyBuilder { HtmlBody = "<b>Hello!</b>", TextBody = "Hello!" };
builder.Attachments.Add("report.pdf", pdfBytes, ContentType.Parse("application/pdf"));
message.Body = builder.ToMessageBody();

// Parse a message
var loaded = MimeMessage.Load("message.eml");
string? text = loaded.TextBody;
string? html = loaded.HtmlBody;
foreach (var attachment in loaded.Attachments.OfType<MimePart>())
{
    using var stream = File.Create(attachment.FileName ?? "file");
    attachment.Content.DecodeTo(stream);
}

// Send (SMTP)
using var smtp = new SmtpClient();
await smtp.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, ct);
await smtp.AuthenticateAsync("user", "pass", ct);
await smtp.SendAsync(message, ct);
await smtp.DisconnectAsync(true, ct);

// Read inbox (IMAP)
using var imap = new ImapClient();
await imap.ConnectAsync("imap.example.com", 993, SecureSocketOptions.SslOnConnect, ct);
await imap.AuthenticateAsync("user", "pass", ct);
await imap.Inbox.OpenAsync(FolderAccess.ReadOnly, ct);
var summaries = await imap.Inbox.FetchAsync(0, -1,
    MessageSummaryItems.Envelope | MessageSummaryItems.Flags, ct);
await imap.DisconnectAsync(true, ct);
```

## Key design decisions

**Use `BodyBuilder` to construct messages.** It manages the multipart tree (mixed/alternative/related) automatically. Build manually only when you need precise MIME structure control.

**Prefer `FetchAsync` over `GetMessageAsync` for listing.** `FetchAsync` with `MessageSummaryItems.Envelope` downloads only headers — far faster for mailbox views.

**Always open folders with minimum required access.** `FolderAccess.ReadOnly` for reading, `FolderAccess.ReadWrite` for flag changes or deletions.

**Use UIDs, not sequence numbers.** Sequence numbers shift when messages are expunged; `UniqueId` stays stable. All bulk operations have UID-based overloads.
