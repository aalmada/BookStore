using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Aspire.Hosting;
using Bogus;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Refit;
using Weasel.Core;
using Weasel.Postgresql;
using AuthorTranslationDto = BookStore.Client.AuthorTranslationDto;
using BookTranslationDto = BookStore.Client.BookTranslationDto;
using CategoryTranslationDto = BookStore.Client.CategoryTranslationDto;
// Resolve ambiguities by preferring Client types
using CreateBookRequest = BookStore.Client.CreateBookRequest;
using UpdateBookRequest = BookStore.Client.UpdateBookRequest;

namespace BookStore.AppHost.Tests;

public static class TestHelpers
{
    static readonly Faker _faker = new();

    public static CreateBookRequest
        GenerateFakeBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null,
            IEnumerable<Guid>? categoryIds = null) => new()
            {
                Title = _faker.Commerce.ProductName(),
                Isbn = _faker.Commerce.Ean13(),
                Language = "en",
                Translations =
            new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new() { Description = _faker.Lorem.Paragraph() },
                ["es"] = new() { Description = _faker.Lorem.Paragraph() }
            },
                PublicationDate = new PartialDate(
            _faker.Date.Past(10).Year,
            _faker.Random.Int(1, 12),
            _faker.Random.Int(1, 28)),
                PublisherId = publisherId,
                AuthorIds = (ICollection<Guid>)(authorIds ?? []),
                CategoryIds = (ICollection<Guid>)(categoryIds ?? []),
                Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
            };

    public static CreateAuthorRequest GenerateFakeAuthorRequest() => new()
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, AuthorTranslationDto>
        {
            ["en"] = new() { Biography = _faker.Lorem.Paragraphs(2) },
            ["es"] = new() { Biography = _faker.Lorem.Paragraphs(2) }
        }
    };

    public static BookStore.Client.UpdateAuthorRequest GenerateFakeUpdateAuthorRequest() => new()
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, AuthorTranslationDto>
        {
            ["en"] = new() { Biography = _faker.Lorem.Paragraphs(2) },
            ["es"] = new() { Biography = _faker.Lorem.Paragraphs(2) }
        }
    };

    public static CreateCategoryRequest GenerateFakeCategoryRequest() => new()
    {
        Translations = new Dictionary<string, CategoryTranslationDto>
        {
            ["en"] = new() { Name = _faker.Commerce.Department(), Description = _faker.Lorem.Sentence() },
            ["es"] = new() { Name = _faker.Commerce.Department(), Description = _faker.Lorem.Sentence() }
        }
    };

    public static BookStore.Client.UpdateCategoryRequest GenerateFakeUpdateCategoryRequest() => new()
    {
        Translations = new Dictionary<string, CategoryTranslationDto>
        {
            ["en"] = new() { Name = _faker.Commerce.Department(), Description = _faker.Lorem.Sentence() },
            ["es"] = new() { Name = _faker.Commerce.Department(), Description = _faker.Lorem.Sentence() }
        }
    };

    public static async Task<CategoryDto> CreateCategoryAsync(ICategoriesClient client, CreateCategoryRequest request)
    {
        CategoryDto? createdCategory = null;
        var received = await ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "CategoryUpdated",
            async () =>
            {
                var response = await client.CreateCategoryWithResponseAsync(request);
                if (response.Error != null)
                {
                    throw response.Error;
                }

                // Read from body directly
                createdCategory = response.Content;
            },
            TestConstants.DefaultEventTimeout);

        if (!received || createdCategory == null)
        {
            throw new Exception("Failed to create category or receive event.");
        }

        return createdCategory!;
    }

    public static async Task UpdateCategoryAsync(ICategoriesClient client, CategoryDto category,
        BookStore.Client.UpdateCategoryRequest request)
    {
        var received = await ExecuteAndWaitForEventAsync(
            category.Id,
            "CategoryUpdated",
            async () => await client.UpdateCategoryAsync(category.Id, request),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive CategoryUpdated event.");
        }
    }

    public static async Task DeleteCategoryAsync(ICategoriesClient client, CategoryDto category)
    {
        var received = await ExecuteAndWaitForEventAsync(
            category.Id,
            "CategoryDeleted",
            async () => await client.SoftDeleteCategoryAsync(category.Id),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive CategoryDeleted event.");
        }
    }

    public static async Task RestoreCategoryAsync(ICategoriesClient client, CategoryDto category)
    {
        var received = await ExecuteAndWaitForEventAsync(
            category.Id,
            "CategoryUpdated",
            async () => await client.RestoreCategoryAsync(category.Id),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive CategoryUpdated event (Restore).");
        }
    }

    public static CreatePublisherRequest GenerateFakePublisherRequest()
        => new() { Name = _faker.Company.CompanyName() };

    public static HttpClient GetAuthenticatedClient(string accessToken)
    {
        var client = GetUnauthenticatedClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static HttpClient GetAuthenticatedClient(string accessToken, string tenantId)
    {
        var client = GetUnauthenticatedClient(tenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        return await Task.FromResult(client);
    }

    public static async Task<T> GetAuthenticatedClientAsync<T>()
    {
        var httpClient = await GetAuthenticatedClientAsync();
        return RestService.For<T>(httpClient);
    }

    public static HttpClient GetUnauthenticatedClient()
        => GetUnauthenticatedClient(StorageConstants.DefaultTenantId);

    public static HttpClient GetUnauthenticatedClient(string tenantId)
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
        return client;
    }

    public static T GetUnauthenticatedClient<T>()
    {
        var httpClient = GetUnauthenticatedClient();
        return RestService.For<T>(httpClient);
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

    public static async Task<AuthorDto> CreateAuthorAsync(IAuthorsClient client,
        BookStore.Client.CreateAuthorRequest createAuthorRequest)
    {
        AuthorDto? createdAuthor = null;

        var received = await ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "AuthorUpdated",
            async () =>
            {
                var response = await client.CreateAuthorWithResponseAsync(createAuthorRequest);
                if (response.Error != null)
                {
                    throw response.Error;
                }

                createdAuthor = response.Content;
            },
            TestConstants.DefaultEventTimeout);

        if (!received || createdAuthor == null)
        {
            throw new Exception("Failed to create author or receive AuthorUpdated event.");
        }

        return createdAuthor;
    }

    public static async Task UpdateAuthorAsync(IAuthorsClient client, AuthorDto author,
        BookStore.Client.UpdateAuthorRequest updateRequest)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorUpdated",
            async () => await client.UpdateAuthorAsync(author.Id, updateRequest),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorUpdated event after UpdateAuthor.");
        }
    }

    public static async Task DeleteAuthorAsync(IAuthorsClient client, AuthorDto author)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorDeleted",
            async () => await client.SoftDeleteAuthorAsync(author.Id),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorDeleted event after DeleteAuthor.");
        }
    }

    public static async Task RestoreAuthorAsync(IAuthorsClient client, AuthorDto author)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorUpdated",
            async () => await client.RestoreAuthorAsync(author.Id),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorUpdated event after RestoreAuthor.");
        }
    }

    public static async Task<BookDto> CreateBookAsync(IBooksClient client, CreateBookRequest createBookRequest)
    {
        Guid? createdId = null;

        var received = await ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "BookUpdated",
            async () =>
            {
                var response = await client.CreateBookWithResponseAsync(createBookRequest);
                if (response.Error != null)
                {
                    throw response.Error;
                }

                // Extract ID from Location header: /api/books/{id}
                var location = response.Headers.Location;
                if (location != null)
                {
                    var segments = location.ToString().TrimEnd('/').Split('/');
                    if (Guid.TryParse(segments.Last(), out var id))
                    {
                        createdId = id;
                    }
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received || createdId == null)
        {
            throw new Exception("Failed to create book or receive BookUpdated event, or extract ID.");
        }

        // Poll with retry to ensure our book is projected.
        var maxRetries = 10;
        var retryDelay = TimeSpan.FromMilliseconds(200);
        BookDto? fetchedBook = null;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                fetchedBook = await client.GetBookAsync(createdId.Value);
                break;
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Continue polling
            }

            if (i < maxRetries - 1)
            {
                await Task.Delay(retryDelay);
            }
        }

        return fetchedBook!;
    }

    public static async Task<PublisherDto> CreatePublisherAsync(IPublishersClient client,
        CreatePublisherRequest request)
    {
        PublisherDto? createdPublisher = null;
        var received = await ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "PublisherUpdated",
            async () =>
            {
                var response = await client.CreatePublisherWithResponseAsync(request);
                if (response.Error != null)
                {
                    throw response.Error;
                }

                // Read from body directly
                createdPublisher = response.Content;
            },
            TestConstants.DefaultEventTimeout);

        if (!received || createdPublisher == null)
        {
            throw new Exception("Failed to create publisher or receive event.");
        }

        return createdPublisher;
    }

    public static async Task UpdatePublisherAsync(IPublishersClient client, PublisherDto publisher,
        UpdatePublisherRequest request)
    {
        var received = await ExecuteAndWaitForEventAsync(
            publisher.Id,
            "PublisherUpdated",
            async () => await client.UpdatePublisherAsync(publisher.Id, request),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive PublisherUpdated event.");
        }
    }

    public static async Task DeletePublisherAsync(IPublishersClient client, PublisherDto publisher)
    {
        var received = await ExecuteAndWaitForEventAsync(
            publisher.Id,
            "PublisherDeleted",
            async () => await client.SoftDeletePublisherAsync(publisher.Id),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive PublisherDeleted event.");
        }
    }

    public static async Task RestorePublisherAsync(IPublishersClient client, PublisherDto publisher)
    {
        var received = await ExecuteAndWaitForEventAsync(
            publisher.Id,
            "PublisherUpdated",
            async () => await client.RestorePublisherAsync(publisher.Id),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive PublisherUpdated event (Restore).");
        }
    }

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, Guid? publisherId = null,
        IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null)
    {
        // Ensure dependencies exist
        if (publisherId == null)
        {
            var pClient = await GetAuthenticatedClientAsync<IPublishersClient>();
            var pub = await CreatePublisherAsync(pClient, GenerateFakePublisherRequest());
            publisherId = pub.Id;
        }

        if (authorIds == null || !authorIds.Any())
        {
            var aClient = await GetAuthenticatedClientAsync<IAuthorsClient>();
            var auth = await CreateAuthorAsync(aClient, GenerateFakeAuthorRequest());
            authorIds = [auth.Id];
        }

        if (categoryIds == null || !categoryIds.Any())
        {
            var cClient = await GetAuthenticatedClientAsync<ICategoriesClient>();
            var cat = await CreateCategoryAsync(cClient, GenerateFakeCategoryRequest());
            categoryIds = [cat.Id];
        }

        var createBookRequest = GenerateFakeBookRequest(publisherId, authorIds, categoryIds);
        return await CreateBookAsync(httpClient, createBookRequest);
    }

    public static async Task<BookDto> CreateBookAsync(IBooksClient client, Guid? publisherId = null,
        IEnumerable<Guid>? authorIds = null, IEnumerable<Guid>? categoryIds = null)
    {
        // Ensure dependencies exist
        if (publisherId == null)
        {
            var pClient = await GetAuthenticatedClientAsync<IPublishersClient>();
            var pub = await CreatePublisherAsync(pClient, GenerateFakePublisherRequest());
            publisherId = pub.Id;
        }

        if (authorIds == null || !authorIds.Any())
        {
            var aClient = await GetAuthenticatedClientAsync<IAuthorsClient>();
            var auth = await CreateAuthorAsync(aClient, GenerateFakeAuthorRequest());
            authorIds = [auth.Id];
        }

        if (categoryIds == null || !categoryIds.Any())
        {
            var cClient = await GetAuthenticatedClientAsync<ICategoriesClient>();
            var cat = await CreateCategoryAsync(cClient, GenerateFakeCategoryRequest());
            categoryIds = [cat.Id];
        }

        var createBookRequest = GenerateFakeBookRequest(publisherId, authorIds, categoryIds);
        return await CreateBookAsync(client, createBookRequest);
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
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for UserUpdated event after AddToCart.");
        }
    }

    public static async Task AddToCartAsync(IShoppingCartClient client, Guid bookId, int quantity = 1,
        Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () => await client.AddToCartAsync(new AddToCartClientRequest(bookId, quantity)),
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

    public static async Task UpdateCartItemQuantityAsync(IShoppingCartClient client, Guid bookId, int quantity,
        Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () => await client.UpdateCartItemAsync(bookId, new UpdateCartItemClientRequest(quantity)),
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

    public static async Task RemoveFromCartAsync(IShoppingCartClient client, Guid bookId, Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () => await client.RemoveFromCartAsync(bookId),
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

    public static async Task ClearCartAsync(IShoppingCartClient client, Guid? expectedEntityId = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            "UserUpdated",
            async () => await client.ClearCartAsync(),
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
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RateBook.");
        }
    }

    public static async Task RateBookAsync(IBooksClient client, Guid bookId, int rating, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () => await client.RateBookAsync(bookId, new RateBookRequest(rating)),
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
                _ = await Assert.That(response.IsSuccessStatusCode).IsTrue();
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveRating.");
        }
    }

    public static async Task RemoveRatingAsync(IBooksClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () => await client.RemoveBookRatingAsync(bookId),
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

    public static async Task AddToFavoritesAsync(IBooksClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () => await client.AddBookToFavoritesAsync(bookId),
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

    public static async Task RemoveFromFavoritesAsync(IBooksClient client, Guid bookId, Guid? expectedEntityId = null,
        string expectedEvent = "UserUpdated")
    {
        var received = await ExecuteAndWaitForEventAsync(
            expectedEntityId ?? Guid.Empty,
            expectedEvent,
            async () => await client.RemoveBookFromFavoritesAsync(bookId),
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

    public static async Task UpdateBookAsync(IBooksClient client, Guid bookId, UpdateBookRequest updatePayload,
        string etag)
    {
        var received = await ExecuteAndWaitForEventAsync(
            bookId,
            "BookUpdated",
            async () => await client.UpdateBookAsync(bookId, updatePayload, etag),
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Timed out waiting for BookUpdated event after UpdateBook.");
        }
    }

    // Helper to accept generic object and cast if possible or use fake request
    public static async Task UpdateBookAsync(IBooksClient client, Guid bookId, object updatePayload, string etag)
    {
        // If updatePayload is anonymous, we can't easily cast it to UpdateBookRequest used by Refit.
        // But the tests typically use TestHelpers.GenerateFakeBookRequest which we changed to return strict type CreateBookRequest.
        // Wait, CreateBookRequest is not UpdateBookRequest.
        // We might need a converter or just update the tests to use proper request type.
        // For now, let's assume we will fix the tests to pass UpdateBookRequest or similar.
        // If we pass an object that JSON serializes to UpdateBookRequest, we'd need to serialize/deserialize.
        // Usage in BookCrudTests: TestHelpers.GenerateFakeBookRequest() -> Returns CreateBookRequest.
        // Does CreateBookRequest match UpdateBookRequest? SImilar properties.
        // I should check if I can map them.
        // Or just change GenerateFakeBookRequest to be generic?
        // Refit expects UpdateBookRequest.
        var json = JsonSerializer.Serialize(updatePayload);
        var request = JsonSerializer.Deserialize<UpdateBookRequest>(json);
        await UpdateBookAsync(client, bookId, request!, etag);
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

    public static async Task DeleteBookAsync(IBooksClient client, Guid bookId, string etag)
    {
        var received = await ExecuteAndWaitForEventAsync(
            bookId,
            "BookDeleted",
            async () => await client.SoftDeleteBookAsync(bookId, etag),
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

    public static async Task RestoreBookAsync(IBooksClient client, Guid bookId, string? etag = null)
    {
        var received = await ExecuteAndWaitForEventAsync(
            bookId,
            "BookUpdated",
            async () => await client.RestoreBookAsync(bookId, apiVersion: "1.0", etag: etag),
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

    public static async Task<LoginResponse?> LoginAsAdminAsync(string tenantId)
    {
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        return await LoginAsAdminAsync(client, tenantId);
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
        var actualTenantId = tenantId ?? StorageConstants.DefaultTenantId;
        publicClient.DefaultRequestHeaders.Add("X-Tenant-ID", actualTenantId);

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
        _ = handler.ReadJwtToken(tokenResponse!.AccessToken);

        // Create authenticated client
        var authenticatedClient = app.CreateHttpClient("apiservice");
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
        authenticatedClient.DefaultRequestHeaders.Add("X-Tenant-ID", actualTenantId);

        return authenticatedClient;
    }

    public static async Task<T> CreateUserAndGetClientAsync<T>(string? tenantId = null)
    {
        var httpClient = await CreateUserAndGetClientAsync(tenantId);
        return RestService.For<T>(httpClient);
    }

    public record LoginResponse(string AccessToken, string RefreshToken);

    public record ErrorResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("error")]
        string Error,
        string Message);

    public record MessageResponse(string Message);

    public record ValidationProblemDetails(
        string? Title = null,
        int? Status = null,
        string? Detail = null,
        [property: System.Text.Json.Serialization.JsonPropertyName("error")]
        string? Error = null);
}
