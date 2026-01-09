using Refit;

namespace BookStore.Client;

public interface IShoppingCartClient :
    IGetShoppingCartEndpoint,
    IAddToCartEndpoint,
    IUpdateCartItemEndpoint,
    IRemoveFromCartEndpoint,
    IClearCartEndpoint
{
}
