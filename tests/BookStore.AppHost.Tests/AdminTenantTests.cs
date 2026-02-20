using System.Net;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Marten;
using Refit;

namespace BookStore.AppHost.Tests;

public class AdminTenantTests
{
    [Test]
    public async Task CreateTenant_WithInvalidPassword_ReturnsBadRequest()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var client =
            RestService.For<ITenantsClient>(HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var command = new CreateTenantCommand(
            Id: "invalid-pwd-tenant",
            Name: "Invalid Pwd Tenant",
            Tagline: "Testing invalid password",
            ThemePrimaryColor: "#ff0000",
            IsEnabled: true,
            AdminEmail: "admin@invalid.com",
            AdminPassword: "short" // Invalid password
        );

        // Act & Assert
        var exception = await Assert.That(async () => await client.CreateTenantAsync(command)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var error = await exception.GetContentAsAsync<AuthenticationHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Tenancy.InvalidAdminPassword);
    }

    [Test]
    public async Task CreateTenant_WithInvalidEmail_ReturnsBadRequest()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var client =
            RestService.For<ITenantsClient>(HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var command = new CreateTenantCommand(
            Id: "invalid-email-tenant",
            Name: "Invalid Email Tenant",
            Tagline: "Testing invalid email",
            ThemePrimaryColor: "#ff0000",
            IsEnabled: true,
            AdminEmail: "invalid-email", // Invalid email
            AdminPassword: FakeDataGenerators.GenerateFakePassword()
        );

        // Act & Assert
        var exception = await Assert.That(async () => await client.CreateTenantAsync(command)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var error = await exception.GetContentAsAsync<AuthenticationHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Tenancy.InvalidAdminEmail);
    }

    [Test]
    public async Task CreateTenant_WithValidRequest_ReturnsCreated()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var client =
            RestService.For<ITenantsClient>(HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var tenantId = $"valid-tenant-{Guid.CreateVersion7():N}";
        var command = new CreateTenantCommand(
            Id: tenantId,
            Name: "Valid Tenant",
            Tagline: "Testing valid creation",
            ThemePrimaryColor: "#00ff00",
            IsEnabled: true,
            AdminEmail: FakeDataGenerators.GenerateFakeEmail(),
            AdminPassword: FakeDataGenerators.GenerateFakePassword() // Valid password
        );

        // Act
        await client.CreateTenantAsync(command);
    }

    [Test]
    public async Task CreateTenant_WithEmailVerification_CreatesUnconfirmedUser()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var client =
            RestService.For<ITenantsClient>(HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var tenantId = $"verify-tenant-{Guid.CreateVersion7():N}";
        var adminEmail = FakeDataGenerators.GenerateFakeEmail();
        var command = new CreateTenantCommand(
            Id: tenantId,
            Name: "Verify Tenant",
            Tagline: "Testing email verification",
            ThemePrimaryColor: "#0000ff",
            IsEnabled: true,
            AdminEmail: adminEmail,
            AdminPassword: FakeDataGenerators.GenerateFakePassword()
        );

        // Act & Assert - Connect to SSE before creating, then wait for notification
        // Creation triggers UserUpdated via UserProfile projection
        var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () => await client.CreateTenantAsync(command),
            TimeSpan.FromSeconds(10));

        _ = await Assert.That(received).IsTrue();

        // Verify side effect: The admin user in the new tenant is created with EmailConfirmed = true
        // because Email:DeliveryMethod=None is configured in GlobalSetup (no email verification required).
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);

        var user = await session.Query<ApplicationUser>()
            .Where(u => u.Email == adminEmail)
            .FirstOrDefaultAsync();

        _ = await Assert.That(user).IsNotNull();
        _ = await Assert.That(user!.EmailConfirmed).IsTrue();
        _ = await Assert.That(user.Roles).Contains("Admin");
    }

    [Test]
    public async Task Admin_CanListAllTenants()
    {
        if (GlobalHooks.App == null || GlobalHooks.AdminAccessToken == null)
        {
            throw new InvalidOperationException("App or AdminAccessToken is not initialized");
        }

        var client =
            RestService.For<ITenantsClient>(HttpClientHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken));

        var result = await client.GetAllTenantsAdminAsync();

        _ = await Assert.That(result).IsNotNull();
        _ = await Assert.That(result).IsNotEmpty();
    }
}
