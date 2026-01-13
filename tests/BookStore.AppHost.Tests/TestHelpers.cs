using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Aspire.Hosting;
using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public static class TestHelpers
{
    static readonly Faker _faker = new();

    public static object GenerateFakeBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null) => new
    {
        Title = _faker.Commerce.ProductName(),
        Isbn = _faker.Commerce.Ean13(),
        Language = "en",
        Translations = new Dictionary<string, object>
        {
            ["en"] = new { Description = _faker.Lorem.Paragraph() },
            ["es"] = new { Description = _faker.Lorem.Paragraph() }
        },
#pragma warning disable IDE0037 // Use target-typed 'new'
        PublicationDate = new
        {
            Year = _faker.Date.Past(10).Year,
            Month = _faker.Random.Int(1, 12),
            Day = _faker.Random.Int(1, 28)
        },
#pragma warning restore IDE0037
        PublisherId = publisherId,
        AuthorIds = authorIds ?? [],
        CategoryIds = categoryIds ?? [],
        Prices = new Dictionary<string, decimal>
        {
            ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100))
        }
    };

    public static object GenerateFakeAuthorRequest() => new
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, object>
        {
            ["en"] = new
            {
                Biography = _faker.Lorem.Paragraphs(2)
            },
            ["es"] = new
            {
                Biography = _faker.Lorem.Paragraphs(2)
            }
        }
    };

    public static object GenerateFakeCategoryRequest() => new
    {
        Translations = new Dictionary<string, object>
        {
            ["en"] = new
            {
                Name = _faker.Commerce.Department(),
                Description = _faker.Lorem.Sentence()
            },
            ["es"] = new
            {
                Name = _faker.Commerce.Department(),
                Description = _faker.Lorem.Sentence()
            }
        }
    };

    public static object GenerateFakePublisherRequest() => new
    {
        Name = _faker.Company.CompanyName()
    };

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
        client.Timeout = TestConstants.DefaultStreamTimeout; // Prevent Aspire default timeout from killing the stream
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
                _ = response.EnsureSuccessStatusCode();
                Console.WriteLine("[SSE-TEST] Connected. Waiting for action to complete before reading stream...");
                _ = connectedTcs.TrySetResult();

                using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

                await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
                {
                    Console.WriteLine($"[SSE-TEST] Received SSE: EventType={item.EventType}, Data={item.Data}");
                    if (string.IsNullOrEmpty(item.Data))
                    {
                        continue;
                    }

                    if (item.EventType == eventType)
                    {
                        using var doc = JsonDocument.Parse(item.Data);
                        if (doc.RootElement.TryGetProperty("entityId", out var idProp))
                        {
                            var receivedId = idProp.GetGuid();
                            if (matchAnyId || receivedId == entityId)
                            {
                                Console.WriteLine($"[SSE-TEST] Match found for {eventType} on {receivedId}!");
                                _ = tcs.TrySetResult(true);
                                return;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[SSE-TEST] Timeout reached waiting for {eventType}.");
                _ = tcs.TrySetResult(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SSE-TEST] Background listener exception: {ex.Message}");
                _ = tcs.TrySetException(ex);
                _ = connectedTcs.TrySetResult(); // Ensure we don't block
            }
        }, cts.Token);

        // Wait for connection to be established
        if (await Task.WhenAny(connectedTcs.Task, Task.Delay(timeout)) != connectedTcs.Task)
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
            _ = response.EnsureSuccessStatusCode();
            Console.WriteLine("[SSE-TEST] Connected. Reading stream...");

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

            await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
            {
                Console.WriteLine($"[SSE-TEST] Received item: {item.EventType}");
                if (string.IsNullOrEmpty(item.Data))
                {
                    continue;
                }

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

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, object createBookRequest)
    {
        BookDto? createdBook = null;

        var received = await ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                _ = createResponse.EnsureSuccessStatusCode();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookDto>();
            },
            TestConstants.DefaultEventTimeout);

        if (!received || createdBook == null)
        {
            throw new Exception("Failed to create book or receive BookUpdated event.");
        }

        return createdBook!;
    }

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, Guid? publisherId = null, IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null)
    {
        var createBookRequest = GenerateFakeBookRequest(publisherId, authorIds, categoryIds);
        return await CreateBookAsync(httpClient, createBookRequest);
    }

    public static async Task AddToCartAsync(HttpClient client, Guid bookId, int quantity = 1, Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(bookId, quantity));
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for UserUpdated event after AddToCart.");
        }
    }

    public static async Task UpdateCartItemQuantityAsync(HttpClient client, Guid bookId, int quantity, Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await client.PutAsJsonAsync($"/api/cart/items/{bookId}", new UpdateCartItemClientRequest(quantity));
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for UserUpdated event after UpdateCartItemQuantity.");
        }
    }

    public static async Task RemoveFromCartAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await client.DeleteAsync($"/api/cart/items/{bookId}");
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for UserUpdated event after RemoveFromCart.");
        }
    }

    public static async Task ClearCartAsync(HttpClient client, Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await client.DeleteAsync("/api/cart");
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for UserUpdated event after ClearCart.");
        }
    }

    public static async Task EnsureCartIsEmptyAsync(HttpClient client)
    {
        var cart = await client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        if (cart != null && cart.TotalItems > 0)
        {
            await ClearCartAsync(client);
        }
    }

    public static async Task RateBookAsync(HttpClient client, Guid bookId, int rating, Guid? expectedEntityId = null, string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.PostAsJsonAsync($"/api/books/{bookId}/rating", new { Rating = rating });
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SSE-TEST] RateBook failed: {response.StatusCode} - {error}");
                }

                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RateBook.");
        }
    }

    public static async Task RemoveRatingAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null, string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.DeleteAsync($"/api/books/{bookId}/rating");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SSE-TEST] RemoveRating failed: {response.StatusCode} - {error}");
                }

                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveRating.");
        }
    }

    public static async Task AddToFavoritesAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null, string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.PostAsync($"/api/books/{bookId}/favorites", null);
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after AddToFavorites.");
        }
    }

    public static async Task RemoveFromFavoritesAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null, string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.DeleteAsync($"/api/books/{bookId}/favorites");
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveFromFavorites.");
        }
    }

    public static async Task UpdateBookAsync(HttpClient client, Guid bookId, object updatePayload, string etag)
    {
        var received = await ExecuteAndWaitForEventAsync(
            bookId,
            "BookUpdated",
            async () =>
            {
                var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/books/{bookId}")
                {
                    Content = JsonContent.Create(updatePayload)
                };
                updateRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

                var updateResponse = await client.SendAsync(updateRequest);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var error = await updateResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Update failed with status {updateResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(updateResponse.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for BookUpdated event after UpdateBook.");
        }
    }

    public static async Task DeleteBookAsync(HttpClient client, Guid bookId, string etag)
    {
        var received = await ExecuteAndWaitForEventAsync(
            bookId,
            "BookDeleted",
            async () =>
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/books/{bookId}");
                deleteRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));

                var deleteResponse = await client.SendAsync(deleteRequest);
                if (!deleteResponse.IsSuccessStatusCode)
                {
                    var error = await deleteResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Delete failed with status {deleteResponse.StatusCode}: {error}");
                }

                _ = await Assert.That(deleteResponse.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for BookDeleted event after DeleteBook.");
        }
    }

    public static async Task RestoreBookAsync(HttpClient client, Guid bookId)
    {
        var received = await ExecuteAndWaitForEventAsync(
            bookId,
            "BookUpdated",
            async () =>
            {
                var restoreResponse = await client.PostAsync($"/api/admin/books/{bookId}/restore", null);
                if (!restoreResponse.IsSuccessStatusCode)
                {
                    var error = await restoreResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SSE-TEST] Restore failed: {restoreResponse.StatusCode} - {error}");
                }

                _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for BookUpdated event after RestoreBook.");
        }
    }
}
