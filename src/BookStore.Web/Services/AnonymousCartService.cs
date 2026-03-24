using Microsoft.JSInterop;

namespace BookStore.Web.Services;

public sealed class AnonymousCartService(IJSRuntime? js)
{
    readonly IJSRuntime? _js = js;

    public event Action? CartChanged;

    public async Task<IReadOnlyList<AnonymousCartItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        if (_js is null)
        {
            return [];
        }

        try
        {
            var items = await _js.InvokeAsync<List<AnonymousCartItemDto>>(
                "anonymousCart.getItems",
                cancellationToken);

            return items
                .Where(item => item.BookId != Guid.Empty)
                .Select(item => new AnonymousCartItem(item.BookId, Math.Clamp(item.Quantity, 1, 10)))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        catch (JSDisconnectedException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<AnonymousCartItem>> AddItemAsync(Guid bookId, int quantity,
        CancellationToken cancellationToken = default)
    {
        var items = await InvokeMutatingMethodAsync("anonymousCart.addItem", [bookId.ToString(), Math.Clamp(quantity, 1, 10)], cancellationToken);
        return items;
    }

    public async Task<IReadOnlyList<AnonymousCartItem>> RemoveItemAsync(Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var items = await InvokeMutatingMethodAsync("anonymousCart.removeItem", [bookId.ToString()], cancellationToken);
        return items;
    }

    public async Task<IReadOnlyList<AnonymousCartItem>> UpdateItemAsync(Guid bookId, int quantity,
        CancellationToken cancellationToken = default)
    {
        var items = await InvokeMutatingMethodAsync("anonymousCart.updateItem", [bookId.ToString(), Math.Clamp(quantity, 1, 10)], cancellationToken);
        return items;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_js is null)
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("anonymousCart.clear", cancellationToken);
            CartChanged?.Invoke();
        }
        catch (InvalidOperationException)
        {
            // JS interop is unavailable during prerendering.
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected; clearing localStorage is best-effort.
        }
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        if (_js is null)
        {
            return 0;
        }

        try
        {
            return await _js.InvokeAsync<int>("anonymousCart.getCount", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
        catch (JSDisconnectedException)
        {
            return 0;
        }
    }

    async Task<IReadOnlyList<AnonymousCartItem>> InvokeMutatingMethodAsync(string identifier, object?[] args,
        CancellationToken cancellationToken)
    {
        if (_js is null)
        {
            return [];
        }

        try
        {
            var items = await _js.InvokeAsync<List<AnonymousCartItemDto>>(identifier, cancellationToken, args);
            CartChanged?.Invoke();
            return items
                .Where(item => item.BookId != Guid.Empty)
                .Select(item => new AnonymousCartItem(item.BookId, Math.Clamp(item.Quantity, 1, 10)))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        catch (JSDisconnectedException)
        {
            return [];
        }
    }

    sealed record AnonymousCartItemDto(Guid BookId, int Quantity);
}

public sealed record AnonymousCartItem(Guid BookId, int Quantity);
