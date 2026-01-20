using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Aspire.Hosting;
using Bogus;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;

namespace BookStore.AppHost.Tests;

public static class TestHelpers
{
    static readonly Faker _faker = new();

    public static object
        GenerateFakeBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null,
            IEnumerable<Guid>? categoryIds = null) => new
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
                Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
            };

    public static object GenerateFakeAuthorRequest() => new
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, object>
        {
            ["en"] = new { Biography = _faker.Lorem.Paragraphs(2) },
            ["es"] = new { Biography = _faker.Lorem.Paragraphs(2) }
        }
    };

    public static object GenerateFakeCategoryRequest() => new
    {
        Translations = new Dictionary<string, object>
        {
            ["en"] = new { Name = _faker.Commerce.Department(), Description = _faker.Lorem.Sentence() },
            ["es"] = new { Name = _faker.Commerce.Department(), Description = _faker.Lorem.Sentence() }
        }
    };

    public static object GenerateFakePublisherRequest() => new { Name = _faker.Company.CompanyName() };

    public static async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        return await Task.FromResult(client);
    }

    public static HttpClient GetUnauthenticatedClient()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        return client;
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

        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        client.Timeout = TestConstants.DefaultStreamTimeout; // Prevent Aspire default timeout from killing the stream
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        if (string.Equals(GlobalHooks.AdminAccessToken, null))
        {
        }

        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);

        using var cts = new CancellationTokenSource(timeout);
        var tcs = new TaskCompletionSource<bool>();
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

                    if (item.EventType == eventType)
                    {
                        using var doc = JsonDocument.Parse(item.Data);
                        if (doc.RootElement.TryGetProperty("entityId", out var idProp))
                        {
                            var receivedId = idProp.GetGuid();
                            if (matchAnyId || receivedId == entityId)
                            {
                                _ = tcs.TrySetResult(true);
                                return;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _ = tcs.TrySetResult(false);
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

        await action();

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

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, object createBookRequest)
    {
        BookDto? createdBook = null;

        var received = await ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated", // Async projections may report as Update regardless of Insert/Update
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                }

                _ = createResponse.EnsureSuccessStatusCode();
                createdBook = await createResponse.Content.ReadFromJsonAsync<BookDto>();
            },
            TestConstants.DefaultEventTimeout);

        if (!received || createdBook == null)
        {
            throw new Exception("Failed to create book or receive BookUpdated event.");
        }

        // SSE received means async projection is active. In parallel tests, we might receive 
        // SSE for a different book, so poll with retry to ensure our book is projected.
        var maxRetries = 10;
        var retryDelay = TimeSpan.FromMilliseconds(200);
        BookDto? fetchedBook = null;

        for (var i = 0; i < maxRetries; i++)
        {
            var getResponse = await httpClient.GetAsync($"/api/books/{createdBook.Id}");
            if (getResponse.IsSuccessStatusCode)
            {
                fetchedBook = await getResponse.Content.ReadFromJsonAsync<BookDto>();
                break;
            }

            if (i < maxRetries - 1)
            {
                await Task.Delay(retryDelay);
            }
        }

        if (fetchedBook == null)
        {
        }

        return fetchedBook ?? createdBook!;
    }

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, Guid? publisherId = null,
        IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null)
    {
        var createBookRequest = GenerateFakeBookRequest(publisherId, authorIds, categoryIds);
        return await CreateBookAsync(httpClient, createBookRequest);
    }

    public static async Task<HttpClient> GetTenantClientAsync(string tenantId, string accessToken)
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        return await Task.FromResult(client);
    }

    public static async Task AddToCartAsync(HttpClient client, Guid bookId, int quantity = 1,
        Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response =
                    await client.PostAsJsonAsync("/api/cart/items", new AddToCartClientRequest(bookId, quantity));
                if (!response.IsSuccessStatusCode)
                {
                }
                else
                {
                }

                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for UserUpdated event after AddToCart.");
        }
    }

    public static async Task UpdateCartItemQuantityAsync(HttpClient client, Guid bookId, int quantity,
        Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await client.PutAsJsonAsync($"/api/cart/items/{bookId}",
                    new UpdateCartItemClientRequest(quantity));
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

    public static async Task RateBookAsync(HttpClient client, Guid bookId, int rating, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.PostAsJsonAsync($"/api/books/{bookId}/rating", new { Rating = rating });
                if (!response.IsSuccessStatusCode)
                {
                }

                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RateBook.");
        }
    }

    public static async Task RemoveRatingAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () =>
            {
                var response = await client.DeleteAsync($"/api/books/{bookId}/rating");
                if (!response.IsSuccessStatusCode)
                {
                }

                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveRating.");
        }
    }

    public static async Task AddToFavoritesAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
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

    public static async Task RemoveFromFavoritesAsync(HttpClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
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
                }

                _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for BookUpdated event after RestoreBook.");
        }
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

                await Task.Delay(500, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Fall through to failure
        }

        throw new Exception($"Timeout waiting for condition: {failureMessage}");
    }

    public static async Task SeedTenantAsync(Marten.IDocumentStore store, string tenantId)
    {
        // 1. Ensure Tenant document exists in Marten's native default bucket (for validation)
        await using (var tenantSession = store.LightweightSession())
        {
            var existingTenant = await tenantSession.LoadAsync<BookStore.ApiService.Models.Tenant>(tenantId);
            if (existingTenant == null)
            {
                tenantSession.Store(new BookStore.ApiService.Models.Tenant
                {
                    Id = tenantId,
                    Name = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                        ? "BookStore"
                        : (char.ToUpper(tenantId[0]) + tenantId[1..] + " Corp"),
                    IsEnabled = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await tenantSession.SaveChangesAsync();
            }
        }

        // 2. Seed Admin User in the tenant's own bucket
        await using var session = store.LightweightSession(tenantId);

        var adminEmail = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? "admin@bookstore.com"
            : $"admin@{tenantId}.com";

        // We still use manual store here as TestHelpers might be used in light setup contexts
        // but we fix the normalization mismatch
        var existingUser = await session.Query<BookStore.ApiService.Models.ApplicationUser>()
            .Where(u => u.Email == adminEmail)
            .FirstOrDefaultAsync();

        if (existingUser == null)
        {
            var adminUser = new BookStore.ApiService.Models.ApplicationUser
            {
                UserName = adminEmail,
                NormalizedUserName = adminEmail.ToUpperInvariant(),
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                Roles = ["Admin"],
                SecurityStamp = Guid.CreateVersion7().ToString("D"),
                ConcurrencyStamp = Guid.CreateVersion7().ToString("D")
            };

            var hasher =
                new Microsoft.AspNetCore.Identity.PasswordHasher<BookStore.ApiService.Models.ApplicationUser>();
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

            session.Store(adminUser);
            await session.SaveChangesAsync();
        }
    }

    public static async Task<LoginResponse?> LoginAsAdminAsync(HttpClient client, string tenantId)
    {
        var email = StorageConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            ? "admin@bookstore.com"
            : $"admin@{tenantId}.com";

        var credentials = new { email, password = "Admin123!" };

        // Simple retry logic
        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/account/login")
            {
                Content = JsonContent.Create(credentials)
            };
            request.Headers.Add("X-Tenant-ID", tenantId);

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginResponse>();
            }

            if (i == 2) // Last attempt
            {
                return null;
            }

            await Task.Delay(500); // Wait before retry
        }

        return null;
    }

    public static async Task<HttpClient> CreateUserAndGetClientAsync(string? tenantId = null)
    {
        var app = GlobalHooks.App!;
        var publicClient = app.CreateHttpClient("apiservice");
        publicClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);

        var email = $"user_{Guid.NewGuid()}@example.com";
        var password = "Password123!";

        // Register
        var registerRequest = new { email, password };
        var registerResponse = await publicClient.PostAsJsonAsync("/account/register", registerRequest);
        if (!registerResponse.IsSuccessStatusCode)
        {
        }

        _ = registerResponse.EnsureSuccessStatusCode();

        // Login
        var loginRequest = new { email, password };
        var loginResponse = await publicClient.PostAsJsonAsync("/account/login", loginRequest);
        if (!loginResponse.IsSuccessStatusCode)
        {
        }

        _ = loginResponse.EnsureSuccessStatusCode();

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Decode JWT to verify claims
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenResponse!.AccessToken);

        // Create authenticated client
        var authenticatedClient = app.CreateHttpClient("apiservice");
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
        authenticatedClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);

        return authenticatedClient;
    }

    public record LoginResponse(string AccessToken, string RefreshToken);
}
