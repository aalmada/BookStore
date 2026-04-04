# Headers and Addresses

## Address types

| Type | Represents |
|------|-----------|
| `MailboxAddress` | A single `Name <addr>` email address |
| `GroupAddress` | A named group containing mailboxes |
| `InternetAddressList` | `IList<InternetAddress>` — the type of `To`, `Cc`, `Bcc`, `From`, `ReplyTo` |

### Creating addresses

```csharp
var mailbox     = new MailboxAddress("Alice", "alice@example.com");
var noName      = new MailboxAddress(string.Empty, "anon@example.com");
var parsed      = MailboxAddress.Parse("Bob <bob@example.com>");
var parsedGroup = InternetAddressList.Parse("Alice <alice@x.com>, Bob <bob@x.com>");
```

### Adding recipients

```csharp
message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
message.To.Add(new MailboxAddress("Bob",   "bob@example.com"));
message.Cc.Add(new MailboxAddress("Carol", "carol@example.com"));
message.Bcc.Add(new MailboxAddress("Dave", "dave@example.com"));
```

### Iterating mailboxes (skip group addresses)

```csharp
foreach (var mailbox in message.To.Mailboxes)
    Console.WriteLine($"{mailbox.Name} — {mailbox.Address}");
```

### Flattening mixed groups + individual addresses

```csharp
IEnumerable<MailboxAddress> AllMailboxes(InternetAddressList list)
{
    foreach (var addr in list)
    {
        if (addr is MailboxAddress mb)
            yield return mb;
        else if (addr is GroupAddress group)
            foreach (var groupMb in group.Members.Mailboxes)
                yield return groupMb;
    }
}
```

---

## Working with headers

### Standard headers via typed properties

```csharp
message.Subject              = "Re: Questions";
message.Date                 = DateTimeOffset.UtcNow;
message.MessageId            = MimeUtils.GenerateMessageId();
message.InReplyTo            = "<original-id@host>";
message.Priority             = MessagePriority.Urgent;
message.Importance           = MessageImportance.High;
```

### Custom and raw headers

```csharp
// Add custom headers
message.Headers.Add("X-Custom-Header", "value");
message.Headers.Add("X-Mailer", "MyApp/1.0");

// Replace a header value
message.Headers[HeaderId.Subject] = "Corrected Subject";

// Read a specific header
string? xCustom = message.Headers["X-Custom-Header"];

// Enumerate all headers
foreach (var header in message.Headers)
    Console.WriteLine($"{header.Field}: {header.Value}");

// Get all values for a multi-value header (e.g. Received)
var received = message.Headers.GetAllValues(HeaderId.Received);

// Remove a header
message.Headers.Remove(HeaderId.XPriority);
```

---

## Constructing a reply message

```csharp
public static MimeMessage BuildReply(MimeMessage original, MailboxAddress from, bool replyToAll)
{
    var reply = new MimeMessage();
    reply.From.Add(from);

    // Reply-To takes precedence over From
    if (original.ReplyTo.Count > 0)
        reply.To.AddRange(original.ReplyTo);
    else if (original.From.Count > 0)
        reply.To.AddRange(original.From);
    else if (original.Sender is not null)
        reply.To.Add(original.Sender);

    if (replyToAll)
    {
        reply.To.AddRange(original.To);
        reply.Cc.AddRange(original.Cc);
    }

    // Prefix Subject with "Re: " if not already present
    reply.Subject = original.Subject?.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) == true
        ? original.Subject
        : "Re: " + original.Subject;

    // Thread headers
    if (!string.IsNullOrEmpty(original.MessageId))
    {
        reply.InReplyTo = original.MessageId;
        foreach (var id in original.References)
            reply.References.Add(id);
        reply.References.Add(original.MessageId);
    }

    // Quote original plain-text body
    var sender = original.Sender ?? original.From.Mailboxes.FirstOrDefault();
    var quotedText = new StringBuilder();
    quotedText.AppendLine($"On {original.Date:f}, {sender?.Name ?? sender?.Address} wrote:");
    using var reader = new StringReader(original.TextBody ?? string.Empty);
    for (string? line; (line = reader.ReadLine()) is not null;)
        quotedText.AppendLine("> " + line);

    reply.Body = new TextPart("plain") { Text = quotedText.ToString() };
    return reply;
}
```

---

## `ContentType` and `MediaType`

```csharp
// Access the content type of any MimeEntity
var ct = entity.ContentType;
Console.WriteLine(ct.MimeType);   // e.g. "text/plain"
Console.WriteLine(ct.MediaType);  // e.g. "text"
Console.WriteLine(ct.MediaSubtype); // e.g. "plain"
Console.WriteLine(ct.Charset);    // e.g. "utf-8"

// Compare
if (ct.IsMimeType("text", "html")) { ... }

// Parse from string
var parsed = ContentType.Parse("application/json; charset=utf-8");
```
