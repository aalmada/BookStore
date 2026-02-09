using Refit;

namespace BookStore.Client;

public interface IBooksClient :
    IGetBooksEndpoint,
    IGetFavoriteBooksEndpoint,
    IGetBookEndpoint,
    IGetAllBooksAdminEndpoint,
    ICreateBookEndpoint,
    IUpdateBookEndpoint,
    ISoftDeleteBookEndpoint,
    IUploadBookCoverEndpoint,
    IAddBookToFavoritesEndpoint,
    IRemoveBookFromFavoritesEndpoint,
    IRateBookEndpoint,
    IRemoveBookRatingEndpoint,
    IScheduleBookSaleEndpoint,
    ICancelBookSaleEndpoint,
    IRestoreBookEndpoint,
    IGetBookAdminEndpoint
{
}
