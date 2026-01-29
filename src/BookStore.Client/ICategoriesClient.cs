using Refit;

namespace BookStore.Client;

public interface ICategoriesClient :
    IGetCategoriesEndpoint,
    IGetCategoryEndpoint,
    ICreateCategoryEndpoint,
    IUpdateCategoryEndpoint,
    ISoftDeleteCategoryEndpoint,
    IRestoreCategoryEndpoint,
    IGetAllCategoriesAdminEndpoint
{
}
