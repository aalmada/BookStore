using Refit;

namespace BookStore.Client;

public interface ISystemClient :
    IRebuildProjectionsEndpoint,
    IGetProjectionStatusEndpoint
{
}
