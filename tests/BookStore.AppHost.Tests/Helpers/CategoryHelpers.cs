using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests.Helpers;

public static class CategoryHelpers
{
    public static async Task<CategoryDto> CreateCategoryAsync(ICategoriesClient client, CreateCategoryRequest request)
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var result = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
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
}
