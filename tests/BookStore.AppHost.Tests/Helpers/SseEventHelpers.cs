using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Aspire.Hosting;
using BookStore.ApiService.Infrastructure.Tenant;
using JasperFx;

namespace BookStore.AppHost.Tests.Helpers;

public static class SseEventHelpers
{
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
        TimeSpan timeout,
        long minVersion = 0,
        DateTimeOffset? minTimestamp = null)
        => (await ExecuteAndWaitForEventWithVersionAsync(entityId, eventType, action, timeout, minVersion,
                minTimestamp))
            .Success;

    public static async Task<EventResult> ExecuteAndWaitForEventWithVersionAsync(
        Guid entityId,
        string eventType,
        Func<Task> action,
        TimeSpan timeout,
        long minVersion = 0,
        DateTimeOffset? minTimestamp = null)
        => await ExecuteAndWaitForEventWithVersionAsync(entityId, [eventType], action, timeout, minVersion,
            minTimestamp);

    public record EventResult(bool Success, long Version);

    public static async Task<bool> ExecuteAndWaitForEventAsync(
        Guid entityId,
        string[] eventTypes,
        Func<Task> action,
        TimeSpan timeout,
        long minVersion = 0,
        DateTimeOffset? minTimestamp = null)
        => (await ExecuteAndWaitForEventWithVersionAsync(entityId, eventTypes, action, timeout, minVersion,
                minTimestamp))
            .Success;

    public static async Task<EventResult> ExecuteAndWaitForEventWithVersionAsync(
        Guid entityId,
        string[] eventTypes,
        Func<Task> action,
        TimeSpan timeout,
        long minVersion = 0,
        DateTimeOffset? minTimestamp = null)
    {
        var matchAnyId = entityId == Guid.Empty;
        var receivedEvents = new List<string>();

        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        client.Timeout = TestConstants.DefaultStreamTimeout; // Prevent Aspire default timeout from killing the stream
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        using var cts = new CancellationTokenSource(timeout);
        var tcs = new TaskCompletionSource<EventResult>();
        var connectedTcs = new TaskCompletionSource();

        // Start listening to SSE stream
        var listenTask = Task.Run(async () =>
        {
            try
            {
                using var response = await client.GetAsync("/api/notifications/stream",
                    HttpCompletionOption.ResponseHeadersRead, cts.Token);
                _ = response.EnsureSuccessStatusCode();

                _ = connectedTcs.TrySetResult();

                using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

                await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
                {
                    if (string.IsNullOrEmpty(item.Data))
                    {
                        continue;
                    }

                    var received = $"Type: {item.EventType}, Data: {item.Data}";
                    receivedEvents.Add(received);

                    if (eventTypes.Contains(item.EventType))
                    {
                        using var doc = JsonDocument.Parse(item.Data);
                        if (doc.RootElement.TryGetProperty("entityId", out var idProp))
                        {
                            var receivedId = idProp.GetGuid();
                            if (matchAnyId || receivedId == entityId)
                            {
                                if (minVersion > 0)
                                {
                                    if (doc.RootElement.TryGetProperty("version", out var versionProp) &&
                                        versionProp.ValueKind == JsonValueKind.Number &&
                                        versionProp.GetInt64() >= minVersion)
                                    {
                                        // Version match
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                if (minTimestamp.HasValue)
                                {
                                    if (doc.RootElement.TryGetProperty("timestamp", out var timestampProp) &&
                                        timestampProp.TryGetDateTimeOffset(out var timestamp) &&
                                        timestamp >= minTimestamp.Value)
                                    {
                                        // Timestamp match
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                long version = 0;
                                if (doc.RootElement.TryGetProperty("version", out var vProp) &&
                                    vProp.ValueKind == JsonValueKind.Number)
                                {
                                    version = vProp.GetInt64();
                                }

                                _ = tcs.TrySetResult(new EventResult(true, version));
                                return;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _ = tcs.TrySetResult(new EventResult(false, 0));
            }
            catch (Exception ex)
            {
                _ = tcs.TrySetException(ex);
                _ = connectedTcs.TrySetResult(); // Ensure we don't block
            }
        }, cts.Token);

        // Wait for connection to be established
        if (await Task.WhenAny(connectedTcs.Task, Task.Delay(timeout)) != connectedTcs.Task)
        {
            // Proceed anyway? Or fail? proceeding might miss event.
        }

        // Execute the action that should trigger the event
        try
        {
            await action();
        }
        catch (Exception)
        {
            throw;
        }

        // Wait for either the event or timeout
        _ = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

        var result = tcs.Task.IsCompleted && tcs.Task.Result.Success ? tcs.Task.Result : new EventResult(false, 0);

        if (!result.Success)
        {
            cts.Cancel(); // Stop listening
        }

        try
        {
            await listenTask; // Ensure cleanup logic runs and we catch any final exceptions
        }
        catch (Exception)
        {
            // Valid to ignore here during cleanup
            await Task.CompletedTask;
        }

        if (result.Success)
        {
            return result;
        }

        return result;
    }

    /// <summary>
    /// Legacy method - waits for an event AFTER it may have already been sent.
    /// Prefer ExecuteAndWaitForEventAsync instead.
    /// </summary>
    [Obsolete("Use ExecuteAndWaitForEventAsync to avoid race conditions")]
    public static async Task<bool> WaitForEventAsync(Guid entityId, string eventType, TimeSpan timeout)
    {
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            using var response = await client.GetAsync("/api/notifications/stream",
                HttpCompletionOption.ResponseHeadersRead, cts.Token);
            _ = response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

            await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
            {
                if (string.IsNullOrEmpty(item.Data))
                {
                    continue;
                }

                if (item.EventType == eventType)
                {
                    using var doc = JsonDocument.Parse(item.Data);
                    if (doc.RootElement.TryGetProperty("entityId", out var idProp) && idProp.GetGuid() == entityId)
                    {
                        return true;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return false;
    }

    public static async Task WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout, string failureMessage)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                if (await condition())
                {
                    return;
                }

                await Task.Delay(TestConstants.DefaultPollingInterval, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Fall through to failure
        }

        throw new Exception($"Timeout waiting for condition: {failureMessage}");
    }
}
