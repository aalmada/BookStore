# IMAP — Reading and Managing Email

## Connect and open the inbox

```csharp
using MailKit.Net.Imap;
using MailKit;

using var client = new ImapClient();
await client.ConnectAsync("imap.example.com", 993, SecureSocketOptions.SslOnConnect, ct);
await client.AuthenticateAsync("user", "password", ct);

var inbox = client.Inbox;
await inbox.OpenAsync(FolderAccess.ReadOnly, ct);   // ReadWrite if mutating
```

---

## Fetching message summaries (efficient)

`FetchAsync` downloads only what you ask for — use it for listing/searching. Avoid `GetMessageAsync` in loops.

```csharp
// Download subject, date, flags for all messages
var summaries = await inbox.FetchAsync(0, -1,
    MessageSummaryItems.UniqueId |
    MessageSummaryItems.Envelope |
    MessageSummaryItems.Flags, ct);

foreach (var s in summaries)
    Console.WriteLine($"[{s.UniqueId}] {s.Envelope.Subject} — {s.Flags}");
```

### Fetch only specific headers (advanced)

```csharp
var request = new FetchRequest(MessageSummaryItems.UniqueId)
{
    Headers = new HeaderSet([HeaderId.Subject, HeaderId.From, HeaderId.Date])
};
var summaries = await inbox.FetchAsync(0, -1, request, ct);
```

---

## Fetching a full message

```csharp
// By UID (preferred — stable across expunges)
var uid = new UniqueId(42);
MimeMessage message = await inbox.GetMessageAsync(uid, ct);

// By index (can shift when messages are expunged)
MimeMessage message = await inbox.GetMessageAsync(0, ct);
```

---

## Searching

`SearchQuery` builds composable queries. Results are `IList<UniqueId>`.

```csharp
using MailKit.Search;

// Unread messages in the last 7 days
var query = SearchQuery.NotSeen
    .And(SearchQuery.DeliveredAfter(DateTime.UtcNow.AddDays(-7)));

var uids = await inbox.SearchAsync(query, ct);

// Read only those messages
var summaries = await inbox.FetchAsync(uids,
    MessageSummaryItems.Envelope | MessageSummaryItems.Flags, ct);
```

### Common `SearchQuery` predicates

| Query | Matches |
|-------|---------|
| `SearchQuery.NotSeen` | Unread messages |
| `SearchQuery.Seen` | Read messages |
| `SearchQuery.Flagged` | Starred/flagged |
| `SearchQuery.SubjectContains("x")` | Subject contains "x" |
| `SearchQuery.FromContains("x")` | From address contains "x" |
| `SearchQuery.DeliveredAfter(date)` | Arrived after date |
| `SearchQuery.DeliveredBefore(date)` | Arrived before date |
| `.And(q)` | Combine queries (AND) |
| `.Or(q)` | Combine queries (OR) |
| `.Not()` | Negate a query |

---

## Setting flags

Flag operations require `FolderAccess.ReadWrite`.

```csharp
await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

// Mark as read
await inbox.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Seen) { Silent = true }, ct);

// Mark as unread
await inbox.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Remove, MessageFlags.Seen) { Silent = true }, ct);

// Mark for deletion
await inbox.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true }, ct);
```

---

## Moving, copying, and deleting

```csharp
// Move to another folder (uses IMAP MOVE extension if available)
var trash = await client.GetFolderAsync(SpecialFolder.Trash, ct);
await inbox.MoveToAsync(uid, trash, ct);

// If the server lacks MOVE/UIDPLUS, MoveTo may not expunge automatically
await inbox.ExpungeAsync(ct);   // force removal of \Deleted messages

// Copy (leaves original in place)
await inbox.CopyToAsync(uid, trash, ct);

// Delete permanently
await inbox.StoreAsync(uid, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true }, ct);
await inbox.ExpungeAsync(ct);
```

---

## Folder navigation

```csharp
// Special folders (Drafts, Sent, Trash, Junk, Archive)
var sent  = await client.GetFolderAsync(SpecialFolder.Sent, ct);
var trash = await client.GetFolderAsync(SpecialFolder.Trash, ct);

// All personal folders
var personal  = client.GetFolder(client.PersonalNamespaces[0]);
var subfolders = await personal.GetSubfoldersAsync(false, ct);

// Heuristic fallback for servers without SPECIAL-USE
static readonly string[] SentNames = ["Sent Items", "Sent Mail", "Sent Messages"];
var sent = subfolders.FirstOrDefault(f => SentNames.Contains(f.Name));
```

---

## POP3 (alternative to IMAP)

Use only when the server doesn't support IMAP. POP3 is download-and-delete; folder management is not possible.

```csharp
using MailKit.Net.Pop3;

using var client = new Pop3Client();
await client.ConnectAsync("pop.example.com", 995, SecureSocketOptions.SslOnConnect, ct);
await client.AuthenticateAsync("user", "pass", ct);

int count = client.Count;
for (int i = 0; i < count; i++)
{
    var message = await client.GetMessageAsync(i, ct);
    // process message...
    await client.DeleteMessageAsync(i, ct);   // mark for deletion
}

await client.DisconnectAsync(true, ct);   // disconnect sends QUIT, deleting marked messages
```
