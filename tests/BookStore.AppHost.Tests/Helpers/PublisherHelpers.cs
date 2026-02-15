using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests.Helpers;

public static class PublisherHelpers
{
    public static async Task<PublisherDto> CreatePublisherAsync(IPublishersClient client,
        CreatePublisherRequest request)
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var result = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
}
