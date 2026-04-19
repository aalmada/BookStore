using BookStore.Shared;
using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace BookStore.Web.Tests.Infrastructure;

public class AuthorizationPolicyExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task AddSystemAdminPolicy_ShouldRequireAdminRoleAndDefaultTenantClaim()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddSystemAdminPolicy();
        var policy = options.GetPolicy(AuthorizationPolicyExtensions.SystemAdminPolicyName);

        // Assert
        _ = await Assert.That(policy).IsNotNull();

        var roleRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().SingleOrDefault();
        _ = await Assert.That(roleRequirement).IsNotNull();
        _ = await Assert.That(roleRequirement!.AllowedRoles.Contains("Admin")).IsTrue();

        var claimRequirement = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .SingleOrDefault(r => r.ClaimType == "tenant_id");

        _ = await Assert.That(claimRequirement).IsNotNull();
        _ = await Assert.That(claimRequirement!.AllowedValues).IsNotNull();
        _ = await Assert.That(claimRequirement.AllowedValues!.Contains(MultiTenancyConstants.DefaultTenantId)).IsTrue();
    }
}
