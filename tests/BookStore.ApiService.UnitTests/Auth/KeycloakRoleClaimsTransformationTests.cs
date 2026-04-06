using System.Security.Claims;
using BookStore.ApiService.Infrastructure.Auth;

namespace BookStore.ApiService.UnitTests.Auth;

public class KeycloakRoleClaimsTransformationTests
{
    readonly KeycloakRoleClaimsTransformation _transformation = new();

    [Test]
    public async Task Transform_WithAdminRole_AddsClaimsTypeRole()
    {
        var principal = CreatePrincipal("""{"roles":["Admin"]}""");

        var transformedPrincipal = await _transformation.TransformAsync(principal);
        var roles = transformedPrincipal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        _ = await Assert.That(roles.Length).IsEqualTo(1);
        _ = await Assert.That(roles.Contains("Admin")).IsTrue();
    }

    [Test]
    public async Task Transform_WithMultipleRoles_AddsAllRoleClaims()
    {
        var principal = CreatePrincipal("""{"roles":["Admin","User","CatalogManager"]}""");

        var transformedPrincipal = await _transformation.TransformAsync(principal);
        var roles = transformedPrincipal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        _ = await Assert.That(roles.Length).IsEqualTo(3);
        _ = await Assert.That(roles.Contains("Admin")).IsTrue();
        _ = await Assert.That(roles.Contains("User")).IsTrue();
        _ = await Assert.That(roles.Contains("CatalogManager")).IsTrue();
    }

    [Test]
    public async Task Transform_WithNoRealmAccess_ReturnsUnchangedPrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "TestAuth"));

        var transformedPrincipal = await _transformation.TransformAsync(principal);

        _ = await Assert.That(ReferenceEquals(transformedPrincipal, principal)).IsTrue();
        _ = await Assert.That(transformedPrincipal.FindAll(ClaimTypes.Role).Any()).IsFalse();
    }

    [Test]
    public async Task Transform_WithEmptyRoles_ReturnsUnchangedPrincipal()
    {
        var principal = CreatePrincipal("""{"roles":[]}""");

        var transformedPrincipal = await _transformation.TransformAsync(principal);

        _ = await Assert.That(ReferenceEquals(transformedPrincipal, principal)).IsTrue();
        _ = await Assert.That(transformedPrincipal.FindAll(ClaimTypes.Role).Any()).IsFalse();
    }

    static ClaimsPrincipal CreatePrincipal(string realmAccessJson)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("realm_access", realmAccessJson)
        ],
        authenticationType: "TestAuth");

        return new ClaimsPrincipal(identity);
    }
}
