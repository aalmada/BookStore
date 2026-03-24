using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Refit;

namespace BookStore.AppHost.Tests;

public class ShoppingCartMergeTests
{
    readonly Faker _faker = new();

    [Test]
    [Category("Integration")]
    public async Task MergeCart_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        using var client = HttpClientHelpers.GetUnauthenticatedClient(MapTenantId("default"));

        var request = new MergeCartRequest(
        [
            new CartItemToMergeRequest(Guid.CreateVersion7(), _faker.Random.Int(1, 3))
        ]);

        // Act
        using var response = await client.PostAsJsonAsync("/api/cart/merge", request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Category("Integration")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WithoutAuthentication_InNonDefaultTenant_ShouldReturnForbidden(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        using var client = HttpClientHelpers.GetUnauthenticatedClient(MapTenantId(tenantId));

        var request = new MergeCartRequest(
        [
            new CartItemToMergeRequest(Guid.CreateVersion7(), _faker.Random.Int(1, 3))
        ]);

        // Act
        using var response = await client.PostAsJsonAsync("/api/cart/merge", request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Category("Integration")]
    [Arguments("default")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WithEmptyItems_ShouldReturnBadRequest(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(MapTenantId(tenantId));

        // Act
        using var response = await userClient.Client.PostAsJsonAsync("/api/cart/merge", new MergeCartRequest([]));
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationHelpers.ValidationProblemDetails>();

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        _ = await Assert.That(problem).IsNotNull();
        _ = await Assert.That(problem!.Error).IsEqualTo(ErrorCodes.Cart.EmptyMerge);
    }

    [Test]
    [Category("Integration")]
    [Arguments("default")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WithOnlyFilteredItems_ShouldReturnBadRequestWithNothingToMerge(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(MapTenantId(tenantId));
        var request = new MergeCartRequest(
        [
            new CartItemToMergeRequest(Guid.CreateVersion7(), _faker.Random.Int(1, 3))
        ]);

        // Act
        using var response = await userClient.Client.PostAsJsonAsync("/api/cart/merge", request);
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationHelpers.ValidationProblemDetails>();

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        _ = await Assert.That(problem).IsNotNull();
        _ = await Assert.That(problem!.Error).IsEqualTo(ErrorCodes.Cart.NothingToMerge);

        var cart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(cart).IsNotNull();
        _ = await Assert.That(cart!.TotalItems).IsEqualTo(0);
        _ = await Assert.That(cart.Items.Count).IsEqualTo(0);
    }

    [Test]
    [Category("Integration")]
    [Arguments("default")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WithValidItems_ShouldUpdateCartProjection(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        var adminClient = await CreateTenantAdminClientAsync(tenantId);
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(MapTenantId(tenantId));
        var createdBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var request = new MergeCartRequest([new CartItemToMergeRequest(createdBook.Id, 2)]);

        // Act
        var received = await MergeCartAndWaitForUserUpdateAsync(userClient, request);

        // Assert
        _ = await Assert.That(received).IsTrue();

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
            return cart is { TotalItems: 2 } &&
                   cart.Items.Count == 1 &&
                   cart.Items[0].BookId == createdBook.Id &&
                   cart.Items[0].Quantity == 2;
        }, TestConstants.DefaultTimeout, "Merged cart projection did not contain the expected item.");

        var finalCart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(finalCart).IsNotNull();
        _ = await Assert.That(finalCart!.Items.Count).IsEqualTo(1);
        _ = await Assert.That(finalCart.Items[0].BookId).IsEqualTo(createdBook.Id);
        _ = await Assert.That(finalCart.Items[0].Quantity).IsEqualTo(2);
        _ = await Assert.That(finalCart.TotalItems).IsEqualTo(2);
    }

    [Test]
    [Category("Integration")]
    [Arguments("default")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WithDuplicateBookIds_ShouldDeduplicateAndSumQuantities(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        var adminClient = await CreateTenantAdminClientAsync(tenantId);
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(MapTenantId(tenantId));
        var createdBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());

        var request = new MergeCartRequest(
        [
            new CartItemToMergeRequest(createdBook.Id, 2),
            new CartItemToMergeRequest(createdBook.Id, 3),
            new CartItemToMergeRequest(createdBook.Id, 1)
        ]);

        // Act
        var received = await MergeCartAndWaitForUserUpdateAsync(userClient, request);

        // Assert
        _ = await Assert.That(received).IsTrue();

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
            return cart is { TotalItems: 6 } && cart.Items.Count == 1 && cart.Items[0].Quantity == 6;
        }, TestConstants.DefaultTimeout, "Duplicate merge items were not deduplicated into a single cart entry.");

        var finalCart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(finalCart).IsNotNull();
        _ = await Assert.That(finalCart!.Items.Count).IsEqualTo(1);
        _ = await Assert.That(finalCart.Items[0].BookId).IsEqualTo(createdBook.Id);
        _ = await Assert.That(finalCart.Items[0].Quantity).IsEqualTo(6);
    }

    [Test]
    [Category("Integration")]
    [Arguments("default")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WhenCombinedQuantityExceedsCap_ShouldCapResultAtTen(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        var adminClient = await CreateTenantAdminClientAsync(tenantId);
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(MapTenantId(tenantId));
        var cartClient = RestService.For<IShoppingCartClient>(userClient.Client);
        var createdBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());

        await ShoppingCartHelpers.AddToCartAsync(cartClient, createdBook.Id, 7, userClient.UserId);

        var request = new MergeCartRequest([new CartItemToMergeRequest(createdBook.Id, 6)]);

        // Act
        var received = await MergeCartAndWaitForUserUpdateAsync(userClient, request);

        // Assert
        _ = await Assert.That(received).IsTrue();

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
            return cart is { TotalItems: 10 } && cart.Items.Count == 1 && cart.Items[0].Quantity == 10;
        }, TestConstants.DefaultTimeout, "Cart quantity was not capped at 10 after merge.");

        var finalCart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(finalCart).IsNotNull();
        _ = await Assert.That(finalCart!.Items[0].Quantity).IsEqualTo(10);
        _ = await Assert.That(finalCart.TotalItems).IsEqualTo(10);
    }

    [Test]
    [Category("Integration")]
    [Arguments("default")]
    [Arguments("tenant-merge")]
    public async Task MergeCart_WithValidItems_ShouldEmitUserUpdatedNotification(string tenantId)
    {
        // Arrange
        await EnsureTenantExistsAsync(tenantId);
        var adminClient = await CreateTenantAdminClientAsync(tenantId);
        var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(MapTenantId(tenantId));
        var firstBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var secondBook = await BookHelpers.CreateBookAsync(adminClient, FakeDataGenerators.GenerateFakeBookRequest());

        var request = new MergeCartRequest(
        [
            new CartItemToMergeRequest(firstBook.Id, 2),
            new CartItemToMergeRequest(secondBook.Id, 4)
        ]);

        // Act
        var received = await MergeCartAndWaitForUserUpdateAsync(userClient, request);

        // Assert
        _ = await Assert.That(received).IsTrue();

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await userClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
            return cart is { TotalItems: 6 } &&
                   cart.Items.Count == 2 &&
                   cart.Items.Any(item => item.BookId == firstBook.Id && item.Quantity == 2) &&
                   cart.Items.Any(item => item.BookId == secondBook.Id && item.Quantity == 4);
        }, TestConstants.DefaultTimeout, "UserUpdated notification did not lead to the merged cart projection becoming visible.");
    }

    [Test]
    [Category("Integration")]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MergeCart_ShouldKeepMergedCartDataIsolatedAcrossTenants(bool sourceUsesDefaultTenant)
    {
        // Arrange
        var nonDefaultTenantId = FakeDataGenerators.GenerateFakeTenantId();
        await EnsureTenantExistsAsync(nonDefaultTenantId);

        var sourceTenantId = sourceUsesDefaultTenant ? StorageConstants.DefaultTenantId : nonDefaultTenantId;
        var otherTenantId = sourceUsesDefaultTenant ? nonDefaultTenantId : StorageConstants.DefaultTenantId;

        var sourceAdminClient = await CreateTenantAdminClientAsync(sourceTenantId);
        var sourceUserClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(sourceTenantId);
        var otherUserClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(otherTenantId);
        var createdBook = await BookHelpers.CreateBookAsync(sourceAdminClient, FakeDataGenerators.GenerateFakeBookRequest());
        var mergedQuantity = _faker.Random.Int(1, 3);
        var mergeRequest = new MergeCartRequest([new CartItemToMergeRequest(createdBook.Id, mergedQuantity)]);

        // Act
        var received = await MergeCartAndWaitForUserUpdateAsync(sourceUserClient, mergeRequest);

        // Assert
        _ = await Assert.That(received).IsTrue();

        await SseEventHelpers.WaitForConditionAsync(async () =>
        {
            var cart = await sourceUserClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
            return cart is { TotalItems: > 0 } &&
                   cart.Items.Any(item => item.BookId == createdBook.Id && item.Quantity == mergedQuantity);
        }, TestConstants.DefaultTimeout, "Merged cart item was not visible in the source tenant.");

        var sourceCart = await sourceUserClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(sourceCart).IsNotNull();
        _ = await Assert.That(sourceCart!.Items.Any(item => item.BookId == createdBook.Id && item.Quantity == mergedQuantity)).IsTrue();

        var otherCart = await otherUserClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(otherCart).IsNotNull();
        _ = await Assert.That(otherCart!.Items.Any(item => item.BookId == createdBook.Id)).IsFalse();
        _ = await Assert.That(otherCart.TotalItems).IsEqualTo(0);

        using var response = await otherUserClient.Client.PostAsJsonAsync("/api/cart/merge", mergeRequest);
        var problem = await response.Content.ReadFromJsonAsync<AuthenticationHelpers.ValidationProblemDetails>();

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        _ = await Assert.That(problem).IsNotNull();
        _ = await Assert.That(problem!.Error).IsEqualTo(ErrorCodes.Cart.NothingToMerge);

        var otherCartAfterMergeAttempt = await otherUserClient.Client.GetFromJsonAsync<ShoppingCartResponse>("/api/cart");
        _ = await Assert.That(otherCartAfterMergeAttempt).IsNotNull();
        _ = await Assert.That(otherCartAfterMergeAttempt!.Items.Any(item => item.BookId == createdBook.Id)).IsFalse();
        _ = await Assert.That(otherCartAfterMergeAttempt.TotalItems).IsEqualTo(0);
    }

    static async Task EnsureTenantExistsAsync(string tenantId)
    {
        if (StorageConstants.DefaultTenantId.Equals(MapTenantId(tenantId), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await DatabaseHelpers.CreateTenantViaApiAsync(MapTenantId(tenantId));
    }

    static async Task<HttpClient> CreateTenantAdminClientAsync(string tenantId)
    {
        var actualTenantId = MapTenantId(tenantId);
        if (StorageConstants.DefaultTenantId.Equals(actualTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return await HttpClientHelpers.GetAuthenticatedClientAsync();
        }

        await DatabaseHelpers.CreateTenantViaApiAsync(actualTenantId);
        var defaultAdminClient = await HttpClientHelpers.GetAuthenticatedClientAsync();
        var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(defaultAdminClient, actualTenantId);

        if (loginResponse == null)
        {
            throw new InvalidOperationException($"Could not login as tenant admin for '{actualTenantId}'.");
        }

        return await HttpClientHelpers.GetTenantClientAsync(actualTenantId, loginResponse.AccessToken);
    }

    static async Task<bool> MergeCartAndWaitForUserUpdateAsync(
        AuthenticationHelpers.UserClient userClient,
        MergeCartRequest request)
    {
        var minTimestamp = DateTimeOffset.UtcNow;

        return await SseEventHelpers.ExecuteAndWaitForEventAsync(
            userClient.UserId,
            "UserUpdated",
            async () =>
            {
                using var response = await userClient.Client.PostAsJsonAsync("/api/cart/merge", request);
                var isExpectedStatus = response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK;
                _ = await Assert.That(isExpectedStatus).IsTrue();
            },
            TestConstants.DefaultEventTimeout,
            minTimestamp: minTimestamp);
    }

    static string MapTenantId(string tenantId)
        => tenantId == "default" ? StorageConstants.DefaultTenantId : tenantId;
}
