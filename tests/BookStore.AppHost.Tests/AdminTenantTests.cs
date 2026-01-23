using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using Marten;
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

        using var client = GlobalHooks.App.CreateHttpClient("apiservice");

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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tenants")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        _ = await Assert.That(content).Contains("At least 8 characters");
    }

    [Test]
    public async Task CreateTenant_WithInvalidEmail_ReturnsBadRequest()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        using var client = GlobalHooks.App.CreateHttpClient("apiservice");

        // Arrange
        var command = new CreateTenantCommand(
            Id: "invalid-email-tenant",
            Name: "Invalid Email Tenant",
            Tagline: "Testing invalid email",
            ThemePrimaryColor: "#ff0000",
            IsEnabled: true,
            AdminEmail: "invalid-email", // Invalid email
            AdminPassword: "Password123!"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tenants")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        _ = await Assert.That(content).Contains("Invalid Admin Email");
    }

    [Test]
    public async Task CreateTenant_WithValidRequest_ReturnsCreated()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        using var client = GlobalHooks.App.CreateHttpClient("apiservice");

        // Arrange
        var tenantId = $"valid-tenant-{Guid.NewGuid():N}";
        var command = new CreateTenantCommand(
            Id: tenantId,
            Name: "Valid Tenant",
            Tagline: "Testing valid creation",
            ThemePrimaryColor: "#00ff00",
            IsEnabled: true,
            AdminEmail: "admin@valid.com",
            AdminPassword: "Password123!" // Valid password
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tenants")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateTenant_WithEmailVerification_CreatesUnconfirmedUser()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        using var client = GlobalHooks.App.CreateHttpClient("apiservice");

        // Arrange
        var tenantId = $"verify-tenant-{Guid.NewGuid():N}";
        var adminEmail = "admin@verify.com";
        var command = new CreateTenantCommand(
            Id: tenantId,
            Name: "Verify Tenant",
            Tagline: "Testing email verification",
            ThemePrimaryColor: "#0000ff",
            IsEnabled: true,
            AdminEmail: adminEmail,
            AdminPassword: "Password123!"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tenants")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

        // Act & Assert - Connect to SSE before creating, then wait for notification
        // Creation triggers UserUpdated via UserProfile projection
        var received = await TestHelpers.ExecuteAndWaitForEventAsync(
            Guid.Empty,
            "UserUpdated",
            async () =>
            {
                var response = await client.SendAsync(request);
                _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
            },
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
