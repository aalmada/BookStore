using System.Linq;
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
// Resolve ambiguities by preferring Client types
using CreateBookRequest = BookStore.Client.CreateBookRequest;
using SharedModels = BookStore.Shared.Models;
using UpdateBookRequest = BookStore.Client.UpdateBookRequest;

namespace BookStore.AppHost.Tests;

public static class TestHelpers
{
    static readonly Faker _faker = new();

    /// <summary>
    /// Generates a random password that meets common password requirements.
    /// </summary>
    /// <returns>A password with at least 12 characters including uppercase, lowercase, numbers, and special characters.</returns>
    public static string GenerateFakePassword() => _faker.Internet.Password(12, false, "", "Aa1!");

    /// <summary>
    /// Generates a random email address for testing.
    /// </summary>
    /// <returns>A valid email address.</returns>
    public static string GenerateFakeEmail() => _faker.Internet.Email();

    /// <summary>
    /// Creates a Marten DocumentStore configured for the BookStore application.
    /// </summary>
    /// <returns>A configured IDocumentStore instance.</returns>
    public static async Task<IDocumentStore> GetDocumentStoreAsync()
    {
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        return DocumentStore.For(opts =>
        {
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });
    }

    /// <summary>
    /// Retrieves a user by email address from the given Marten session.
    /// </summary>
    public static async Task<BookStore.ApiService.Models.ApplicationUser?> GetUserByEmailAsync(
        Marten.IQuerySession session,
        string email) => await session.Query<BookStore.ApiService.Models.ApplicationUser>()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

    /// <summary>
    /// Registers a new user and logs them in, returning complete authentication details.
    /// </summary>
    public static async Task<(string Email, string Password, LoginResponse Login, string TenantId)>
        RegisterAndLoginUserAsync(string? tenantId = null)
    {
        tenantId ??= StorageConstants.DefaultTenantId;
        var email = GenerateFakeEmail();
        var password = GenerateFakePassword();

        var client = GetUnauthenticatedClient(tenantId);
        var registerResponse = await client.PostAsJsonAsync("/account/register", new { email, password });
        _ = registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/account/login", new { email, password });
        _ = loginResponse.EnsureSuccessStatusCode();

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Login response was null.");
        }

        return (email, password, tokenResponse, tenantId);
    }

    /// <summary>
    /// Generates a fake book creation request with random data using Bogus.
    /// </summary>
    /// <param name="publisherId">Optional publisher ID. If null, the book will have no publisher.</param>
    /// <param name="authorIds">Optional collection of author IDs. If null or empty, the book will have no authors.</param>
    /// <param name="categoryIds">Optional collection of category IDs. If null or empty, the book will have no categories.</param>
    /// <returns>A CreateBookRequest with randomized title, ISBN, translations, and prices.</returns>
    public static CreateBookRequest
        GenerateFakeBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null,
            IEnumerable<Guid>? categoryIds = null) => new()
            {
                Id = Guid.CreateVersion7(),
                Title = _faker.Commerce.ProductName(),
                Isbn = _faker.Commerce.Ean13(),
                Language = "en",
                Translations =
            new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new(_faker.Lorem.Paragraph()),
                ["es"] = new(_faker.Lorem.Paragraph())
            },
                PublicationDate = new PartialDate(
            _faker.Date.Past(10).Year,
            _faker.Random.Int(1, 12),
            _faker.Random.Int(1, 28)),
                PublisherId = publisherId,
                AuthorIds = [.. (authorIds ?? [])],
                CategoryIds = [.. (categoryIds ?? [])],
                Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
            };

    public static UpdateBookRequest
        GenerateFakeUpdateBookRequest(Guid? publisherId = null, IEnumerable<Guid>? authorIds = null,
            IEnumerable<Guid>? categoryIds = null) => new()
            {
                Title = _faker.Commerce.ProductName(),
                Isbn = _faker.Commerce.Ean13(),
                Language = "en",
                Translations =
            new Dictionary<string, BookTranslationDto>
            {
                ["en"] = new(_faker.Lorem.Paragraph()),
                ["es"] = new(_faker.Lorem.Paragraph())
            },
                PublicationDate = new PartialDate(
            _faker.Date.Past(10).Year,
            _faker.Random.Int(1, 12),
            _faker.Random.Int(1, 28)),
                PublisherId = publisherId,
                AuthorIds = [.. (authorIds ?? [])],
                CategoryIds = [.. (categoryIds ?? [])],
                Prices = new Dictionary<string, decimal> { ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100)) }
            };

    /// <summary>
    /// Generates a fake author creation request with random data using Bogus.
    /// </summary>
    /// <returns>A CreateAuthorRequest with randomized name and biography in English and Spanish.</returns>
    public static CreateAuthorRequest GenerateFakeAuthorRequest() => new()
    {
        Id = Guid.CreateVersion7(),
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, AuthorTranslationDto>
        {
            ["en"] = new(_faker.Lorem.Paragraphs(2)),
            ["es"] = new(_faker.Lorem.Paragraphs(2))
        }
    };

    public static BookStore.Client.UpdateAuthorRequest GenerateFakeUpdateAuthorRequest() => new()
    {
        Name = _faker.Name.FullName(),
        Translations = new Dictionary<string, AuthorTranslationDto>
        {
            ["en"] = new(_faker.Lorem.Paragraphs(2)),
            ["es"] = new(_faker.Lorem.Paragraphs(2))
        }
    };

    /// <summary>
    /// Generates a fake category creation request with random data using Bogus.
    /// </summary>
    /// <returns>A CreateCategoryRequest with randomized name and description in English and Spanish.</returns>
    public static CreateCategoryRequest GenerateFakeCategoryRequest() => new()
    {
        Id = Guid.CreateVersion7(),
        Translations = new Dictionary<string, CategoryTranslationDto>
        {
            ["en"] = new(_faker.Commerce.Department()),
            ["es"] = new(_faker.Commerce.Department())
        }
    };

    /// <summary>
    /// Generates a fake category update request with random data using Bogus.
    /// </summary>
    /// <returns>An UpdateCategoryRequest with randomized name and description in English and Spanish.</returns>
    public static BookStore.Client.UpdateCategoryRequest GenerateFakeUpdateCategoryRequest() => new()
    {
        Translations = new Dictionary<string, CategoryTranslationDto>
        {
            ["en"] = new(_faker.Commerce.Department()),
            ["es"] = new(_faker.Commerce.Department())
        }
    };

    public static async Task<CategoryDto> CreateCategoryAsync(ICategoriesClient client, CreateCategoryRequest request)
    {
        var received = await ExecuteAndWaitForEventAsync(
            request.Id,
            ["CategoryCreated", "CategoryUpdated"],
            async () =>
            {
                var response = await client.CreateCategoryWithResponseAsync(request);
                if (response.Error != null)
                {
                    throw response.Error;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive CategoryCreated event.");
        }

        return await client.GetCategoryAsync(request.Id);
    }

    public static async Task<CategoryDto> UpdateCategoryAsync(ICategoriesClient client, CategoryDto category,
        UpdateCategoryRequest request)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(category.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            category.Id,
            "CategoryUpdated",
            async () => await client.UpdateCategoryAsync(category.Id, request, category.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive CategoryUpdated event.");
        }

        return await client.GetCategoryAsync(category.Id);
    }

    public static async Task<AdminCategoryDto> UpdateCategoryAsync(ICategoriesClient client, AdminCategoryDto category,
        UpdateCategoryRequest request)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(category.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            category.Id,
            "CategoryUpdated",
            async () => await client.UpdateCategoryAsync(category.Id, request, category.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive CategoryUpdated event.");
        }

        return await client.GetCategoryAdminAsync(category.Id);
    }

    public static async Task<CategoryDto> DeleteCategoryAsync(ICategoriesClient client, CategoryDto category)
    {
        var result = await ExecuteAndWaitForEventWithVersionAsync(
            category.Id,
            "CategoryDeleted",
            async () => await client.SoftDeleteCategoryAsync(category.Id, category.ETag),
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!result.Success)
        {
            throw new Exception("Failed to receive CategoryDeleted event.");
        }

        try
        {
            return await client.GetCategoryAsync(category.Id);
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Soft-deleted, hidden from public API. Construct DTO with reconstructed ETag.
            return category with { ETag = $"\"{result.Version}\"" };
        }
    }

    public static async Task<CategoryDto> RestoreCategoryAsync(ICategoriesClient client, CategoryDto category)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(category.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            category.Id,
            "CategoryUpdated",
            async () => await client.RestoreCategoryAsync(category.Id, category.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive CategoryUpdated event (Restore).");
        }

        return await client.GetCategoryAsync(category.Id);
    }

    /// <summary>
    /// Generates a fake publisher creation request with random data using Bogus.
    /// </summary>
    /// <returns>A CreatePublisherRequest with a randomized company name.</returns>
    public static CreatePublisherRequest GenerateFakePublisherRequest()
        => new() { Id = Guid.CreateVersion7(), Name = _faker.Company.CompanyName() };

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

    /// <summary>
    /// Gets an authenticated HTTP client for the API service using the global admin token.
    /// </summary>
    /// <typeparam name="T">The Refit interface type to create a client for.</typeparam>
    /// <returns>A Refit client instance configured with admin authentication.</returns>
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

    public static T GetUnauthenticatedClientWithLanguage<T>(string language)
    {
        var httpClient = GetUnauthenticatedClient();
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
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

    public static async Task<BookDto> CreateBookAsync(HttpClient httpClient, object createBookRequest)
    {
        // Try to get Id from the request object if it's one of ours
        var entityId = Guid.Empty;
        if (createBookRequest is CreateBookRequest req)
        {
            entityId = req.Id;
        }

        var received = await ExecuteAndWaitForEventAsync(
            entityId,
            [
                "BookCreated", "BookUpdated"
            ], // Async projections may report as Update regardless of Insert/Update
            async () =>
            {
                var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                }

                _ = createResponse.EnsureSuccessStatusCode();
                if (entityId == Guid.Empty)
                {
                    var createdBook = await createResponse.Content.ReadFromJsonAsync<BookDto>();
                    entityId = createdBook?.Id ?? Guid.Empty;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received || entityId == Guid.Empty)
        {
            throw new Exception("Failed to create book or receive BookUpdated event.");
        }

        return (await httpClient.GetFromJsonAsync<BookDto>($"/api/books/{entityId}"))!;
    }

    public static async Task<AuthorDto> CreateAuthorAsync(IAuthorsClient client,
        CreateAuthorRequest createAuthorRequest)
    {
        var received = await ExecuteAndWaitForEventAsync(
            createAuthorRequest.Id,
            ["AuthorCreated", "AuthorUpdated"],
            async () =>
            {
                var response = await client.CreateAuthorWithResponseAsync(createAuthorRequest);
                if (response.Error != null)
                {
                    throw response.Error;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorCreated event.");
        }

        return await client.GetAuthorAsync(createAuthorRequest.Id);
    }

    public static async Task<AuthorDto> UpdateAuthorAsync(IAuthorsClient client, AuthorDto author,
        UpdateAuthorRequest updateRequest)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(author.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            author.Id,
            "AuthorUpdated",
            async () => await client.UpdateAuthorAsync(author.Id, updateRequest, author.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive AuthorUpdated event after UpdateAuthor.");
        }

        return await client.GetAuthorAsync(author.Id);
    }

    public static async Task<AdminAuthorDto> UpdateAuthorAsync(IAuthorsClient client, AdminAuthorDto author,
        UpdateAuthorRequest updateRequest)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(author.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            author.Id,
            "AuthorUpdated",
            async () => await client.UpdateAuthorAsync(author.Id, updateRequest, author.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive AuthorUpdated event after UpdateAuthor.");
        }

        return await client.GetAuthorAdminAsync(author.Id);
    }

    public static async Task<AuthorDto> DeleteAuthorAsync(IAuthorsClient client, AuthorDto author)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorDeleted",
            async () =>
            {
                var etag = author.ETag;
                if (string.IsNullOrEmpty(etag))
                {
                    var latestAuthor = await client.GetAuthorAdminAsync(author.Id);
                    etag = latestAuthor?.ETag;
                }

                await client.SoftDeleteAuthorAsync(author.Id, etag);
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorDeleted event after DeleteAuthor.");
        }

        return await client.GetAuthorAsync(author.Id);
    }

    public static async Task<AdminAuthorDto> DeleteAuthorAsync(IAuthorsClient client, AdminAuthorDto author)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorDeleted",
            async () =>
            {
                var etag = author.ETag;
                if (string.IsNullOrEmpty(etag))
                {
                    var latestAuthor = await client.GetAuthorAdminAsync(author.Id);
                    etag = latestAuthor?.ETag;
                }

                await client.SoftDeleteAuthorAsync(author.Id, etag);
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorDeleted event after DeleteAuthor.");
        }

        return await client.GetAuthorAdminAsync(author.Id);
    }

    public static async Task<AuthorDto> RestoreAuthorAsync(IAuthorsClient client, AuthorDto author)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorUpdated",
            async () =>
            {
                var latestAuthor = await client.GetAuthorAdminAsync(author.Id);
                var etag = latestAuthor?.ETag;

                await client.RestoreAuthorAsync(author.Id, etag);
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorUpdated event after RestoreAuthor.");
        }

        return await client.GetAuthorAsync(author.Id);
    }

    public static async Task<AdminAuthorDto> RestoreAuthorAsync(IAuthorsClient client, AdminAuthorDto author)
    {
        var received = await ExecuteAndWaitForEventAsync(
            author.Id,
            "AuthorUpdated",
            async () =>
            {
                var latestAuthor = await client.GetAuthorAdminAsync(author.Id);
                var etag = latestAuthor?.ETag;

                await client.RestoreAuthorAsync(author.Id, etag);
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive AuthorUpdated event after RestoreAuthor.");
        }

        return await client.GetAuthorAdminAsync(author.Id);
    }

    public static async Task<BookDto> CreateBookAsync(IBooksClient client, CreateBookRequest createBookRequest)
    {
        var received = await ExecuteAndWaitForEventAsync(
            createBookRequest.Id,
            ["BookCreated", "BookUpdated"],
            async () =>
            {
                var response = await client.CreateBookWithResponseAsync(createBookRequest);
                if (response.Error != null)
                {
                    throw response.Error;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive BookCreated event.");
        }

        return await client.GetBookAsync(createBookRequest.Id);
    }

    public static async Task<PublisherDto> CreatePublisherAsync(IPublishersClient client,
        CreatePublisherRequest request)
    {
        var received = await ExecuteAndWaitForEventAsync(
            request.Id,
            ["PublisherCreated", "PublisherUpdated"],
            async () =>
            {
                var response = await client.CreatePublisherWithResponseAsync(request);
                if (response.Error != null)
                {
                    throw response.Error;
                }
            },
            TestConstants.DefaultEventTimeout);

        if (!received)
        {
            throw new Exception("Failed to receive PublisherCreated event.");
        }

        var result = await client.GetAllPublishersAsync(new PublisherSearchRequest { Search = request.Name });
        return result!.Items.First(p => p.Id == request.Id);
    }

    public static async Task<PublisherDto> UpdatePublisherAsync(IPublishersClient client, PublisherDto publisher,
        UpdatePublisherRequest request)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(publisher.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            publisher.Id,
            "PublisherUpdated",
            async () => await client.UpdatePublisherAsync(publisher.Id, request, publisher.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive PublisherUpdated event.");
        }

        return await client.GetPublisherAsync(publisher.Id);
    }

    public static async Task<PublisherDto> DeletePublisherAsync(IPublishersClient client, PublisherDto publisher)
    {
        var result = await ExecuteAndWaitForEventWithVersionAsync(
            publisher.Id,
            "PublisherDeleted",
            async () => await client.SoftDeletePublisherAsync(publisher.Id, publisher.ETag),
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!result.Success)
        {
            throw new Exception("Failed to receive PublisherDeleted event.");
        }

        try
        {
            return await client.GetPublisherAsync(publisher.Id);
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Soft-deleted, hidden from public API. Construct DTO with reconstructed ETag.
            return publisher with { ETag = $"\"{result.Version}\"" };
        }
    }

    public static async Task<PublisherDto> RestorePublisherAsync(IPublishersClient client, PublisherDto publisher)
    {
        var received = await ExecuteAndWaitForEventAsync(
            publisher.Id,
            "PublisherUpdated",
            async () => await client.RestorePublisherAsync(publisher.Id, publisher.ETag),
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception("Failed to receive PublisherUpdated event (Restore).");
        }

        return await client.GetPublisherAsync(publisher.Id);
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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RateBook.");
        }
    }

    public static async Task RateBookAsync(IBooksClient client, Guid bookId, int rating, Guid expectedEntityId,
        string expectedEvent) => await ExecuteAndWaitForEventAsync(
        expectedEntityId,
        expectedEvent,
        () => client.RateBookAsync(bookId, new RateBookRequest(rating)),
        TimeSpan.FromSeconds(10), // Increased timeout
        minTimestamp: DateTimeOffset.UtcNow); // Use current time to avoid stale events

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveRating.");
        }
    }

    public static async Task RemoveRatingAsync(IBooksClient client, Guid bookId, Guid expectedEntityId,
        string expectedEvent) => await ExecuteAndWaitForEventAsync(
        expectedEntityId,
        expectedEvent,
        () => client.RemoveBookRatingAsync(bookId),
        TimeSpan.FromSeconds(10),
        minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            async () => await client.AddBookToFavoritesAsync(bookId,
                null),
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

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
            async () =>
            {
                var book = await client.GetBookAsync(bookId);
                await client.RemoveBookFromFavoritesAsync(bookId, book?.ETag);
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received)
        {
            throw new Exception($"Timed out waiting for {expectedEvent} event after RemoveFromFavorites.");
        }
    }

    public static async Task UpdateBookAsync(HttpClient client, Guid bookId, object updatePayload, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
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
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after UpdateBook.");
        }
    }

    public static async Task<BookDto> UpdateBookAsync(IBooksClient client, Guid bookId, UpdateBookRequest updatePayload,
        string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            "BookUpdated",
            async () => await client.UpdateBookAsync(bookId, updatePayload, etag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after UpdateBook.");
        }

        return await client.GetBookAsync(bookId);
    }

    public static async Task<AdminBookDto> UpdateBookAsync(IBooksClient client, AdminBookDto book,
        UpdateBookRequest request)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(book.ETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            book.Id,
            "BookUpdated",
            async () => await client.UpdateBookAsync(book.Id, request, book.ETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Failed to receive BookUpdated event.");
        }

        return await client.GetBookAdminAsync(book.Id);
    }

    // Helper to accept generic object and cast if possible or use fake request
    public static async Task<BookDto> UpdateBookAsync(IBooksClient client, Guid bookId, object updatePayload,
        string etag)
    {
        var json = JsonSerializer.Serialize(updatePayload);
        var request = JsonSerializer.Deserialize<UpdateBookRequest>(json);
        return await UpdateBookAsync(client, bookId, request!, etag);
    }

    public static async Task DeleteBookAsync(HttpClient client, Guid bookId, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
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
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookDeleted event after DeleteBook.");
        }
    }

    public static async Task<AdminBookDto> DeleteBookAsync(IBooksClient client, Guid bookId, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            ["BookDeleted", "BookSoftDeleted"],
            async () => await client.SoftDeleteBookAsync(bookId, etag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookSoftDeleted event after DeleteBook.");
        }

        return await client.GetBookAdminAsync(bookId);
    }

    public static async Task<AdminBookDto> DeleteBookAsync(IBooksClient client, BookDto book)
    {
        var etag = book.ETag;
        if (string.IsNullOrEmpty(etag))
        {
            var latest = await client.GetBookAsync(book.Id);
            etag = latest.ETag;
        }

        return await DeleteBookAsync(client, book.Id, etag!);
    }

    public static async Task RestoreBookAsync(HttpClient client, Guid bookId, string etag)
    {
        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(etag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            ["BookUpdated", "BookRestored"],
            async () =>
            {
                var restoreResponse = await client.PostAsync($"/api/admin/books/{bookId}/restore", null);
                if (!restoreResponse.IsSuccessStatusCode)
                {
                }

                _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            },
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after RestoreBook.");
        }
    }

    public static async Task<BookDto> RestoreBookAsync(IBooksClient client, Guid bookId, string? etag = null)
    {
        var currentETag = etag;
        if (string.IsNullOrEmpty(currentETag))
        {
            // Use Admin endpoint to get the book, including soft-deleted ones, to get the ETag
            var book = await client.GetBookAdminAsync(bookId);
            currentETag = book?.ETag;
            Console.WriteLine($"[TestHelpers] Fetched ETag for restore: {currentETag}");
        }

        var version = BookStore.ApiService.Infrastructure.ETagHelper.ParseETag(currentETag) ?? 0;
        var received = await ExecuteAndWaitForEventWithVersionAsync(
            bookId,
            ["BookUpdated", "BookRestored"],
            async () => await client.RestoreBookAsync(bookId, apiVersion: "1.0", etag: currentETag),
            TestConstants.DefaultEventTimeout,
            minVersion: version + 1,
            minTimestamp: DateTimeOffset.UtcNow);

        if (!received.Success)
        {
            throw new Exception("Timed out waiting for BookUpdated event after RestoreBook.");
        }

        return await client.GetBookAsync(bookId);
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

            await Task.Delay(TestConstants.DefaultPollingInterval); // Wait before retry
        }

        return null;
    }

    public static async Task<UserClient> CreateUserAndGetClientAsync(string? tenantId = null)
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

        var userId = Guid.Parse(handler.ReadJwtToken(tokenResponse!.AccessToken).Claims.First(c => c.Type == "sub")
            .Value);

        // Create authenticated client
        var authenticatedClient = app.CreateHttpClient("apiservice");
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
        authenticatedClient.DefaultRequestHeaders.Add("X-Tenant-ID", actualTenantId);

        return new UserClient(authenticatedClient, userId);
    }

    public record UserClient(HttpClient Client, Guid UserId);

    public static async Task<T> CreateUserAndGetClientAsync<T>(string? tenantId = null)
    {
        var userClient = await CreateUserAndGetClientAsync(tenantId);
        return RestService.For<T>(userClient.Client);
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
