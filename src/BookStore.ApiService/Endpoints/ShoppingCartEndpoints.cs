using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace BookStore.ApiService.Endpoints;

public static class ShoppingCartEndpoints
{
    public static void MapShoppingCartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cart")
            .WithTags("Shopping Cart")
            .RequireAuthorization();

        _ = group.MapGet("/", GetCart)
            .WithName("GetShoppingCart");

        _ = group.MapPost("/items", AddToCart)
            .WithName("AddToCart");

        _ = group.MapPut("/items/{bookId}", UpdateCartItem)
            .WithName("UpdateCartItem");

        _ = group.MapDelete("/items/{bookId}", RemoveFromCart)
            .WithName("RemoveFromCart");

        _ = group.MapDelete("/", ClearCart)
            .WithName("ClearCart");
    }

    static async Task<Results<Ok<ShoppingCartDto>, NotFound>> GetCart(
        [FromServices] IQuerySession session,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();

        if (userId == Guid.Empty)
        {
            return TypedResults.NotFound();
        }

        // Read from the UserProfile projection document (same pattern as Books/Categories)
        // The async projection has updated this document after processing cart events
        var profile = await session.LoadAsync<UserProfile>(userId, cancellationToken);

        if (profile == null)
        {
            // UserProfile doesn't exist - return empty cart
            return TypedResults.Ok(new ShoppingCartDto([], 0));
        }

        if (profile.ShoppingCartItems?.Count == 0)
        {
            return TypedResults.Ok(new ShoppingCartDto([], 0));
        }

        var bookIds = profile.ShoppingCartItems!.Keys.ToList();
        var books = await session.Query<BookSearchProjection>()
            .Where(b => bookIds.Contains(b.Id) && !b.Deleted)
            .ToListAsync(cancellationToken);

        var items = books.Select(book => new ShoppingCartItemDto(
            book.Id,
            book.Title ?? "Unknown",
            book.Isbn,
            profile.ShoppingCartItems[book.Id],
            book.Prices)).ToList();

        var cart = new ShoppingCartDto(
            items,
            items.Sum(i => i.Quantity));

        return TypedResults.Ok(cart);
    }

    static async Task<Results<NoContent, NotFound, BadRequest<string>>> AddToCart(
        [FromBody] AddToCartRequest request,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return TypedResults.BadRequest("Quantity must be greater than 0");
        }

        var userId = context.User.GetUserId();

        if (userId == Guid.Empty)
        {
            return TypedResults.NotFound();
        }

        await bus.InvokeAsync(new AddBookToCart(userId, request.BookId, request.Quantity), cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<Results<NoContent, NotFound, BadRequest<string>>> UpdateCartItem(
        Guid bookId,
        [FromBody] UpdateCartItemRequest request,
        [FromServices] IMessageBus bus,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return TypedResults.BadRequest("Quantity must be greater than 0");
        }

        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return TypedResults.NotFound();
        }

        await bus.InvokeAsync(new UpdateCartItemQuantity(userId, bookId, request.Quantity), cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<Results<NoContent, NotFound>> RemoveFromCart(
        Guid bookId,
        [FromServices] IMessageBus bus,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return TypedResults.NotFound();
        }

        await bus.InvokeAsync(new RemoveBookFromCart(userId, bookId), cancellationToken);

        return TypedResults.NoContent();
    }

    static async Task<Results<NoContent, NotFound>> ClearCart(
        [FromServices] IMessageBus bus,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return TypedResults.NotFound();
        }

        await bus.InvokeAsync(new ClearShoppingCart(userId), cancellationToken);

        return TypedResults.NoContent();
    }
}
