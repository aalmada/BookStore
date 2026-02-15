using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using TUnit.Assertions.Extensions;

namespace BookStore.AppHost.Tests.Helpers;

public static class ShoppingCartHelpers
{
    public static async Task AddToCartAsync(HttpClient client, Guid bookId, int quantity = 1,
        Guid? expectedEntityId = null)
    {
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
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
}
