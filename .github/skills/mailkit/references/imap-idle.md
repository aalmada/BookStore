# IMAP IDLE — Push Notifications

IMAP IDLE lets the server push real-time notifications (new mail, deleted messages, flag changes) without polling. The client sends the `IDLE` command and the server sends unsolicited responses until the client sends `DONE`.

> **Check capability first**: `client.Capabilities.HasFlag(ImapCapabilities.Idle)`. Fall back to polling if not supported.

---

## Core event subscription

Subscribe to folder events **before** entering IDLE. Events fire on the connection thread while idling.

```csharp
inbox.CountChanged   += OnCountChanged;   // new messages (or messages removed via EXPUNGE)
inbox.MessageExpunged += OnMessageExpunged; // message deleted from the server
inbox.FlagsChanged   += OnFlagsChanged;   // flag change on existing message
```

---

## Full IDLE loop

```csharp
using System.Threading;
using MailKit;
using MailKit.Net.Imap;

async Task RunIdleLoopAsync(CancellationToken appToken)
{
    using var client = new ImapClient();
    await client.ConnectAsync("imap.example.com", 993, SecureSocketOptions.SslOnConnect, appToken);
    await client.AuthenticateAsync(username, password, appToken);

    var inbox = client.Inbox;
    await inbox.OpenAsync(FolderAccess.ReadOnly, appToken);

    inbox.CountChanged    += (s, e) => Console.WriteLine($"Count changed: {((IMailFolder)s!).Count}");
    inbox.MessageExpunged += (s, e) => Console.WriteLine($"Expunged at index {e.Index}");
    inbox.FlagsChanged    += (s, e) => Console.WriteLine($"Flags changed at index {e.Index}");

    while (!appToken.IsCancellationRequested)
    {
        using var doneSource = new CancellationTokenSource();

        // Cancel IDLE after 9 minutes — RFC 2177 recommends re-issuing every 29 min;
        // 9 min gives a comfortable safety margin.
        using var idleTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(9));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(idleTimeout.Token, appToken);

        try
        {
            if (client.Capabilities.HasFlag(ImapCapabilities.Idle))
            {
                // IdleAsync blocks until doneSource is cancelled or the appToken fires
                await client.IdleAsync(doneSource.Token, linked.Token);
            }
            else
            {
                // Polling fallback — check every 30 s
                await Task.Delay(TimeSpan.FromSeconds(30), linked.Token);
                await inbox.CheckAsync(appToken);
            }
        }
        catch (OperationCanceledException) when (idleTimeout.IsCancellationRequested)
        {
            // Re-issue IDLE (loop continues)
        }
    }

    await client.DisconnectAsync(true, CancellationToken.None);
}
```

---

## Stopping IDLE from another thread

`ImapClient.Idle`/`IdleAsync` blocks the connection thread. To stop it externally, cancel the `doneToken`:

```csharp
using var doneSource = new CancellationTokenSource();

// Start IDLE on a background task
var idleTask = Task.Run(() => client.Idle(doneSource.Token, ct));

// ... later, to stop IDLE (e.g., to send a FETCH command):
doneSource.Cancel();
await idleTask;

// Now the connection is free — issue commands, then re-enter IDLE
```

---

## Checking `IsIdle`

Use `client.IsIdle` to query state:

```csharp
if (client.IsIdle)
{
    // Currently idling — you must stop IDLE before sending commands
}
```

---

## Reacting to CountChanged with a FETCH

A typical pattern: when the count increases, fetch the new summaries.

```csharp
int messagesInFolder = inbox.Count;

inbox.CountChanged += async (sender, e) =>
{
    var folder = (IMailFolder)sender!;
    if (folder.Count <= messagesInFolder)
    {
        messagesInFolder = folder.Count; // expunge
        return;
    }
    // Fetch new message summaries (avoid GetMessageAsync in a hot loop)
    var startIndex = messagesInFolder;
    messagesInFolder = folder.Count;

    // Must stop IDLE before issuing FETCH — see "Stopping IDLE" section
    // (Use ImmediateIdle pattern: cancel doneToken, await idle, fetch, re-enter)
};
```

---

## Polling fallback (no IDLE support)

```csharp
while (!ct.IsCancellationRequested)
{
    await Task.Delay(TimeSpan.FromSeconds(30), ct);
    await inbox.CheckAsync(ct);   // sends NOOP + processes unsolicited responses
}
```
