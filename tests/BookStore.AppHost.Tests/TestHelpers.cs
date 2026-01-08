using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.ServerSentEvents;
using Aspire.Hosting;

namespace BookStore.AppHost.Tests;

public static class TestHelpers
{
    public static async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        return await Task.FromResult(client);
    }

    public static HttpClient GetUnauthenticatedClient()
    {
        var app = GlobalHooks.App!;
        return app.CreateHttpClient("apiservice");
    }

    /// <summary>
    /// Executes an action while listening for a specific SSE event.
    /// This ensures the SSE client is connected BEFORE the action is performed,
    /// simulating a real client that's already listening for changes.
    /// </summary>
    /// <param name="entityId">The entity ID to match, or Guid.Empty to match any entity</param>
    /// <param name="eventType">The event type to listen for (e.g., "CategoryCreated")</param>
    /// <param name="action">The action to perform (e.g., create/update/delete)</param>
    /// <param name="timeout">How long to wait for the event</param>
    public static async Task<bool> ExecuteAndWaitForEventAsync(
        Guid entityId,
        string eventType,
        Func<Task> action,
        TimeSpan timeout)
    {
        var matchAnyId = entityId == Guid.Empty;
        Console.WriteLine($"[SSE-TEST] Setting up listener for {eventType}" + 
            (matchAnyId ? " (any ID)" : $" on {entityId}") + 
            $" (timeout: {timeout.TotalSeconds}s)...");
        
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        client.Timeout = TimeSpan.FromMinutes(5); // Prevent Aspire default timeout from killing the stream
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

        using var cts = new CancellationTokenSource(timeout);
        var tcs = new TaskCompletionSource<bool>();
        var connectedTcs = new TaskCompletionSource();

        // Start listening to SSE stream
        var listenTask = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine("[SSE-TEST] Connecting to /api/notifications/stream...");
                using var response = await client.GetAsync("/api/notifications/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();
                Console.WriteLine("[SSE-TEST] Connected. Waiting for action to complete before reading stream...");
                connectedTcs.TrySetResult();

                using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

                await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
                {
                    Console.WriteLine($"[SSE-TEST] Received SSE: EventType={item.EventType}, Data={item.Data}");
                    if (string.IsNullOrEmpty(item.Data)) continue;

                    if (item.EventType == eventType)
                    {
                        using var doc = JsonDocument.Parse(item.Data);
                        if (doc.RootElement.TryGetProperty("entityId", out var idProp))
                        {
                            var receivedId = idProp.GetGuid();
                            if (matchAnyId || receivedId == entityId)
                            {
                                Console.WriteLine($"[SSE-TEST] Match found for {eventType} on {receivedId}!");
                                tcs.TrySetResult(true);
                                return;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[SSE-TEST] Timeout reached waiting for {eventType}.");
                tcs.TrySetResult(false);
            }
            catch (Exception ex)
            {
               Console.WriteLine($"[SSE-TEST] Background listener exception: {ex.Message}");
                tcs.TrySetException(ex);
                connectedTcs.TrySetResult(); // Ensure we don't block
            }
        }, cts.Token);

        // Wait for connection to be established
        if (await Task.WhenAny(connectedTcs.Task, Task.Delay(15000)) != connectedTcs.Task)
        {
             Console.WriteLine("[SSE-TEST] Timed out waiting for SSE connection.");
             // Proceed anyway? Or fail? proceeding might miss event.
        }

        // Execute the action that should trigger the event
        Console.WriteLine($"[SSE-TEST] Executing action...");
        await action();
        Console.WriteLine($"[SSE-TEST] Action completed. Waiting for event...");

        // Wait for either the event or timeout
        var result = await tcs.Task;
        cts.Cancel(); // Stop listening
        
        try
        {
            await listenTask; // Ensure cleanup logic runs and we catch any final exceptions
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        #pragma warning disable RCS1075 // Avoid empty catch clause
        catch (Exception)
        {
            // Valid to ignore here during cleanup
        }
        #pragma warning restore RCS1075

        return result;
    }

    /// <summary>
    /// Legacy method - waits for an event AFTER it may have already been sent.
    /// Prefer ExecuteAndWaitForEventAsync instead.
    /// </summary>
    [Obsolete("Use ExecuteAndWaitForEventAsync to avoid race conditions")]
    public static async Task<bool> WaitForEventAsync(Guid entityId, string eventType, TimeSpan timeout)
    {
        Console.WriteLine($"[SSE-TEST] Waiting for {eventType} on {entityId} (timeout: {timeout.TotalSeconds}s)...");
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            // Console.WriteLine("[SSE-TEST] Connecting to /api/notifications/stream...");
            using var response = await client.GetAsync("/api/notifications/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
            Console.WriteLine("[SSE-TEST] Connected. Reading stream...");

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            
            await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
            {
                Console.WriteLine($"[SSE-TEST] Received item: {item.EventType}");
                if (string.IsNullOrEmpty(item.Data)) continue;

                if (item.EventType == eventType)
                {
                    using var doc = JsonDocument.Parse(item.Data);
                    if (doc.RootElement.TryGetProperty("entityId", out var idProp) && idProp.GetGuid() == entityId)
                    {
                        Console.WriteLine($"[SSE-TEST] Match found for {eventType} on {entityId}!");
                        return true;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[SSE-TEST] Timeout reached waiting for {eventType} on {entityId}.");
            return false;
        }

        return false;
    }
}
