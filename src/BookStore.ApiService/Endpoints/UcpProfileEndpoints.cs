using BookStore.ApiService.Infrastructure.UCP;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookStore.ApiService.Endpoints;

public static class UcpProfileEndpoints
{
    public static IEndpointRouteBuilder MapUcpProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet("/.well-known/ucp", GetProfile)
            .WithName("GetUcpProfile")
            .WithSummary("UCP Business Profile — capability discovery endpoint")
            .WithTags("UCP")
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    static Ok<object> GetProfile(UcpProfileService profileService)
        => TypedResults.Ok(profileService.BuildProfile());
}
