using BookStore.Client;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests.Helpers;

public static class AuthorHelpers
{
    public static async Task<AuthorDto> CreateAuthorAsync(IAuthorsClient client,
        CreateAuthorRequest createAuthorRequest)
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
}
