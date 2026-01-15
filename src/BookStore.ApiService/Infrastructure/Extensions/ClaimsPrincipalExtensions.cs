using System.Security.Claims;

namespace BookStore.ApiService.Infrastructure.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim?.Value, out var id) ? id : Guid.Empty;
    }
}
