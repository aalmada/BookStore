using Refit;

namespace BookStore.Client;

public interface IAuthorsClient :
    IGetAuthorsEndpoint,
    IGetAuthorEndpoint,
    ICreateAuthorEndpoint,
    IUpdateAuthorEndpoint,
    ISoftDeleteAuthorEndpoint,
    IRestoreAuthorEndpoint
{
}
