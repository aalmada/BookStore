using Refit;

namespace BookStore.Client;

public interface IIdentityClient :
    IIdentityLoginEndpoint,
    IIdentityRegisterEndpoint,
    IIdentityConfirmEmailEndpoint,
    IIdentityRefreshEndpoint,
    IIdentityLogoutEndpoint,
    IIdentityChangePasswordEndpoint,
    IIdentityAddPasswordEndpoint,
    IIdentityGetPasswordStatusEndpoint
{
}
