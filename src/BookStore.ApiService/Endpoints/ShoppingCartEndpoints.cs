using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Messages.Commands;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.Shared.Models;
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

    const int MaxQuantityPerItem = 10;

    static async Task<IResult> GetCart(
        [FromServices] IQuerySession session,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();

        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Cart.UserNotFound, "User not found")).ToProblemDetails();
        }

        // Read from the UserProfile projection document (same pattern as Books/Categories)
        // The async projection has updated this document after processing cart events
        var profile = await session.LoadAsync<UserProfile>(userId, cancellationToken);

        if (profile == null)
        {
            // UserProfile doesn't exist - return empty cart
            return Results.Ok(new ShoppingCartDto([], 0));
        }

        if (profile.ShoppingCartItems?.Count == 0)
        {
            return Results.Ok(new ShoppingCartDto([], 0));
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

        return Results.Ok(cart);
    }

    static async Task<IResult> AddToCart(
        [FromBody] AddToCartRequest request,
        [FromServices] IMessageBus bus,
        [FromServices] IQuerySession session,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Cart.InvalidQuantity, "Quantity must be greater than 0")).ToProblemDetails();
        }

        if (request.Quantity > MaxQuantityPerItem)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Cart.QuantityExceeded, $"Quantity cannot exceed {MaxQuantityPerItem}")).ToProblemDetails();
        }

        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Cart.UserNotFound, "User not found")).ToProblemDetails();
        }

        // Validate book exists and is not deleted
        var book = await session.LoadAsync<BookSearchProjection>(request.BookId, cancellationToken);
        if (book == null || book.Deleted)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Cart.BookNotFound, "Book not found")).ToProblemDetails();
        }

        await bus.InvokeAsync(new AddBookToCart(userId, request.BookId, request.Quantity), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return Results.NoContent();
    }

    static async Task<IResult> UpdateCartItem(
        Guid bookId,
        [FromBody] UpdateCartItemRequest request,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Cart.InvalidQuantity, "Quantity must be greater than 0")).ToProblemDetails();
        }

        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Cart.UserNotFound, "User not found")).ToProblemDetails();
        }

        await bus.InvokeAsync(new UpdateCartItemQuantity(userId, bookId, request.Quantity), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return Results.NoContent();
    }

    static async Task<IResult> RemoveFromCart(
        Guid bookId,
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Cart.UserNotFound, "User not found")).ToProblemDetails();
        }

        await bus.InvokeAsync(new RemoveBookFromCart(userId, bookId), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return Results.NoContent();
    }

    static async Task<IResult> ClearCart(
        [FromServices] IMessageBus bus,
        [FromServices] ITenantContext tenantContext,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Cart.UserNotFound, "User not found")).ToProblemDetails();
        }

        await bus.InvokeAsync(new ClearShoppingCart(userId), new DeliveryOptions { TenantId = tenantContext.TenantId }, cancellationToken);

        return Results.NoContent();
    }
}
