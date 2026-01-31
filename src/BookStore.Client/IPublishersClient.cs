using Refit;

namespace BookStore.Client;

public interface IPublishersClient :
    IGetPublishersEndpoint,
    IGetPublisherEndpoint,
    ICreatePublisherEndpoint,
    IUpdatePublisherEndpoint,
    ISoftDeletePublisherEndpoint,
    IRestorePublisherEndpoint,
    IGetAllPublishersAdminEndpoint
{
}
