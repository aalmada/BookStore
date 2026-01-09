using Refit;

namespace BookStore.Client;

public interface IBooksClient :
    IGetBooksEndpoint,
    IGetBookEndpoint,
    IGetAllBooksAdminEndpoint,
    ICreateBookEndpoint,
    IUpdateBookEndpoint,
    ISoftDeleteBookEndpoint,
    IUploadBookCoverEndpoint,
    IAddBookToFavoritesEndpoint,
    IRemoveBookFromFavoritesEndpoint,
    IRateBookEndpoint,
    IRemoveBookRatingEndpoint
{
}
