# Parsing MIME Messages

## Loading a message

```csharp
// From file path
var message = MimeMessage.Load("message.eml");

// From stream
using var stream = File.OpenRead("message.eml");
var message = MimeMessage.Load(stream);

// Async
var message = await MimeMessage.LoadAsync("message.eml");
using var stream = File.OpenRead("message.eml");
var message = await MimeMessage.LoadAsync(stream);
```

---

## Accessing body text

```csharp
// TextBody and HtmlBody walk the MIME tree and find the first text/plain or text/html part
string? textBody = message.TextBody;   // null if not present
string? htmlBody = message.HtmlBody;   // null if not present
```

---

## Iterating all body parts

When you need to walk the entire MIME tree (not just the first text body):

```csharp
foreach (var entity in message.BodyParts)
{
    if (entity is TextPart textPart && !textPart.IsAttachment)
    {
        if (textPart.IsHtml)
            Console.WriteLine("HTML: " + textPart.Text);
        else
            Console.WriteLine("Plain: " + textPart.Text);
    }
}
```

---

## Accessing attachments

`message.Attachments` enumerates only `MimeEntity` nodes whose `Content-Disposition` is `attachment` (or where `IsAttachment` is `true`).

```csharp
foreach (var attachment in message.Attachments)
{
    string fileName = attachment.ContentDisposition?.FileName
                   ?? attachment.ContentType.Name
                   ?? "unnamed";

    if (attachment is MimePart part)
    {
        // Regular file attachment — decode and save
        using var stream = File.Create(Path.GetFileName(fileName));
        part.Content.DecodeTo(stream);
    }
    else if (attachment is MessagePart rfc822)
    {
        // Embedded email attachment — save as .eml
        using var stream = File.Create("embedded.eml");
        rfc822.Message.WriteTo(stream);
    }
}
```

### Getting attachment bytes in memory

```csharp
if (attachment is MimePart mimePart)
{
    using var memStream = new MemoryStream();
    mimePart.Content.DecodeTo(memStream);
    byte[] bytes = memStream.ToArray();
}
```

### Opening attachment content as a stream

```csharp
using var decoded = mimePart.Content.Open();
// decoded is a readable, decoded Stream — pass to image loaders, parsers, etc.
```

---

## Advanced: MimeIterator

Use `MimeIterator` when you need to walk the entire MIME tree including attachments *and* their parent multipart (e.g., to remove attachments from a message).

```csharp
var attachments = new List<MimePart>();
var parents     = new List<Multipart>();

var iter = new MimeIterator(message);
while (iter.MoveNext())
{
    if (iter.Parent is Multipart parent && iter.Current is MimePart part && part.IsAttachment)
    {
        parents.Add(parent);
        attachments.Add(part);
    }
}

// Remove each attachment from its parent
for (int i = 0; i < attachments.Count; i++)
    parents[i].Remove(attachments[i]);
```

---

## Common message properties

```csharp
message.MessageId          // Message-ID header (string)
message.Subject            // Subject header
message.Date               // DateTimeOffset
message.From               // InternetAddressList
message.To                 // InternetAddressList
message.Cc                 // InternetAddressList
message.Bcc                // InternetAddressList
message.ReplyTo            // InternetAddressList
message.Sender             // MailboxAddress (single sender)
message.InReplyTo          // string (references parent message)
message.References         // MessageIdList
message.Priority           // MessagePriority enum
message.Importance         // MessageImportance enum
```
