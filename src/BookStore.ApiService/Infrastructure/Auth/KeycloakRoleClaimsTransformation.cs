using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace BookStore.ApiService.Infrastructure.Auth;

public sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var roles = GetRoles(identity);
        if (roles.Count == 0)
        {
            return Task.FromResult(principal);
        }

        var existingRoles = new HashSet<string>(
            identity.FindAll(ClaimTypes.Role).Select(c => c.Value),
            StringComparer.OrdinalIgnoreCase);

        var clonedIdentity = new ClaimsIdentity(identity);
        foreach (var role in roles)
        {
            if (!existingRoles.Contains(role))
            {
                clonedIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.FromResult(new ClaimsPrincipal(clonedIdentity));
    }

    static HashSet<string> GetRoles(ClaimsIdentity identity)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Handle top-level "roles" claim produced by the oidc-usermodel-realm-role-mapper
        // (the format used by the BookStore Keycloak realm configuration).
        foreach (var claim in identity.FindAll("roles"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                _ = roles.Add(claim.Value);
            }
        }

        // Handle flat realm_access.roles claims
        foreach (var claim in identity.FindAll("realm_access.roles"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                _ = roles.Add(claim.Value);
            }
        }

        var realmAccessClaim = identity.FindFirst("realm_access");
        if (realmAccessClaim == null || string.IsNullOrWhiteSpace(realmAccessClaim.Value))
        {
            return roles;
        }

        try
        {
            using var document = JsonDocument.Parse(realmAccessClaim.Value);
            if (!document.RootElement.TryGetProperty("roles", out var rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return roles;
            }

            foreach (var element in rolesElement.EnumerateArray())
            {
                var roleName = element.GetString();
                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    _ = roles.Add(roleName);
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed claims and preserve the original principal.
        }

        return roles;
    }
}
