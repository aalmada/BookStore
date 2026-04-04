# Creating MIME Messages

## NuGet Setup

```xml
<PackageReference Include="MimeKit" />
```

---

## BodyBuilder (recommended)

`BodyBuilder` is the easiest way to compose messages. It builds the correct multipart tree automatically.

```csharp
var message = new MimeMessage();
message.From.Add(new MailboxAddress("Joey", "joey@example.com"));
message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
message.Subject = "Meeting invitation";

var builder = new BodyBuilder();
builder.TextBody = "Hi Alice,\n\nSee attached.";
builder.HtmlBody = "<p>Hi Alice,</p><p>See attached.</p>";

// Attachment — accepts file path, Stream, or byte[] overloads
builder.Attachments.Add("invite.ics");
builder.Attachments.Add("report.pdf", pdfBytes, ContentType.Parse("application/pdf"));

message.Body = builder.ToMessageBody();
```

### Inline/embedded images

```csharp
var builder = new BodyBuilder();

// Generate a content ID for the image to reference in HTML
var contentId = MimeUtils.GenerateMessageId();
builder.HtmlBody = $@"<p>Hello!</p><img src=""cid:{contentId}"" alt=""logo"" />";

// Add as a linked resource (not a download attachment)
var logo = builder.LinkedResources.Add("logo.png");
logo.ContentId = contentId;

message.Body = builder.ToMessageBody();
```

---

## Manual multipart construction

Use when you need explicit control over the MIME structure (e.g., custom content types, multipart/signed).

### Plain text with file attachment

```csharp
var body = new TextPart("plain") { Text = "See attached." };

var attachment = new MimePart("application", "pdf")
{
    Content = new MimeContent(File.OpenRead("report.pdf")),
    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
    ContentTransferEncoding = ContentEncoding.Base64,
    FileName = "report.pdf"
};

var multipart = new Multipart("mixed");
multipart.Add(body);
multipart.Add(attachment);

message.Body = multipart;
```

### multipart/alternative (plain + HTML)

```csharp
var plain = new TextPart("plain") { Text = "Fallback plain text" };
var html  = new TextPart("html")  { Text = "<p>Rich HTML body</p>" };

var alternative = new MultipartAlternative();
alternative.Add(plain);
alternative.Add(html);   // clients pick the last format they support

message.Body = alternative;
```

---

## Serialising / writing messages

```csharp
// Write to file
message.WriteTo("message.eml");

// Write to stream
using var stream = File.OpenWrite("message.eml");
message.WriteTo(stream);

// Write to stream (async)
await message.WriteToAsync(stream);
```

---

## Key types

| Type | Purpose |
|------|---------|
| `MimeMessage` | Root envelope: headers + body |
| `BodyBuilder` | Fluent helper for composing body/attachments |
| `TextPart` | `text/plain` or `text/html` leaf node |
| `MimePart` | Binary leaf node (images, files) |
| `Multipart` | Container for multiple parts |
| `MultipartAlternative` | `multipart/alternative` container |
| `MimeContent` | Wraps a `Stream` inside a `MimePart` |
| `ContentDisposition` | `inline` or `attachment` + filename |
| `ContentEncoding` | `Base64`, `QuotedPrintable`, `SevenBit`, etc. |
| `MimeUtils` | Utility: `GenerateMessageId()`, etc. |
