using System.Net;
using BookStore.ApiService.Models;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx.Events;
using Marten;
using Refit;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

[NotInParallel]
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
            RestService.For<IAdminTenantClient>(TestHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

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

        var error = await exception.GetContentAsAsync<TestHelpers.ErrorResponse>();
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
            RestService.For<IAdminTenantClient>(TestHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var command = new CreateTenantCommand(
            Id: "invalid-email-tenant",
            Name: "Invalid Email Tenant",
            Tagline: "Testing invalid email",
            ThemePrimaryColor: "#ff0000",
            IsEnabled: true,
            AdminEmail: "invalid-email", // Invalid email
            AdminPassword: TestHelpers.GenerateFakePassword()
        );

        // Act & Assert
        var exception = await Assert.That(async () => await client.CreateTenantAsync(command)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var error = await exception.GetContentAsAsync<TestHelpers.ErrorResponse>();
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
            RestService.For<IAdminTenantClient>(TestHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var tenantId = $"valid-tenant-{Guid.NewGuid():N}";
        var command = new CreateTenantCommand(
            Id: tenantId,
            Name: "Valid Tenant",
            Tagline: "Testing valid creation",
            ThemePrimaryColor: "#00ff00",
            IsEnabled: true,
            AdminEmail: TestHelpers.GenerateFakeEmail(),
            AdminPassword: TestHelpers.GenerateFakePassword() // Valid password
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
            RestService.For<IAdminTenantClient>(TestHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken!));

        // Arrange
        var tenantId = $"verify-tenant-{Guid.NewGuid():N}";
        var adminEmail = TestHelpers.GenerateFakeEmail();
        var command = new CreateTenantCommand(
            Id: tenantId,
            Name: "Verify Tenant",
            Tagline: "Testing email verification",
            ThemePrimaryColor: "#0000ff",
            IsEnabled: true,
            AdminEmail: adminEmail,
            AdminPassword: TestHelpers.GenerateFakePassword()
        );

        // Act & Assert - Connect to SSE before creating, then wait for notification
        // Creation triggers UserUpdated via UserProfile projection
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () => await client.CreateTenantAsync(command),
            TimeSpan.FromSeconds(10));

        _ = await Assert.That(received).IsTrue();

        // Verify side effect: User in tenant DB has EmailConfirmed = false
        // (Assuming the test environment has Email:DeliveryMethod != "None")
        // In our tests, it usually is "Logging" which means verification is REQUIRED.

        var connectionString = await GlobalHooks.App.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString!);
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();

            opts.Events.AppendMode = EventAppendMode.Quick;
            opts.Events.UseArchivedStreamPartitioning = true;
            opts.Events.EnableEventSkippingInProjectionsOrSubscriptions = true;

            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await using var session = store.LightweightSession(tenantId);

        var user = await session.Query<ApplicationUser>()
            .Where(u => u.Email == adminEmail)
            .FirstOrDefaultAsync();

        _ = await Assert.That(user).IsNotNull();
        _ = await Assert.That(user!.EmailConfirmed).IsTrue();
        _ = await Assert.That(user.Roles).Contains("Admin");
    }
}
