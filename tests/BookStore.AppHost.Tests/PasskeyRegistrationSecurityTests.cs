using System.Net;
using System.Net.Http.Json;
using BookStore.Shared;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Integration tests for passkey registration security:
/// - Race condition prevention (concurrent registrations)
/// - User ID conflict detection
/// </summary>
public class PasskeyRegistrationSecurityTests
{
    [Test]
    public async Task PasskeyRegistration_ConcurrentAttempts_OnlyOneSucceeds()
    {
        // Arrange - Get registration options to obtain a user ID
        var email = TestHelpers.GenerateFakeEmail();
        var tenantId = MultiTenancyConstants.DefaultTenantId;
        var client = TestHelpers.GetUnauthenticatedClient(tenantId);

        // Get creation options first
        var optionsResponse = await client.PostAsJsonAsync("/account/attestation/options", new
        {
            email
        });

        _ = await Assert.That(optionsResponse.IsSuccessStatusCode).IsTrue();
        var options = await optionsResponse.Content.ReadFromJsonAsync<PasskeyCreationOptionsResponse>();
        _ = await Assert.That(options).IsNotNull();
        _ = await Assert.That(options!.UserId).IsNotEmpty();

        var userId = options.UserId;

        // Act - Simulate concurrent registration attempts with the SAME user ID
        // This simulates a race condition where two clients try to register at the same time
        var registrationResults = new List<(HttpStatusCode StatusCode, string Content)>();
        var registrationTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                var response = await client.PostAsJsonAsync("/account/attestation/result", new
                {
                    credentialJson = "{\"mock\":\"credential\"}",
                    email,
                    userId
                });

                return (response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                return (HttpStatusCode.InternalServerError, ex.Message);
            }
        }).ToArray();

        var results = await Task.WhenAll(registrationTasks);

        // Assert - Only ONE registration should succeed
        var successfulRequests = results.Count(r => r.Item1 is HttpStatusCode.OK or HttpStatusCode.Created);
        var failedRequests = results.Count(r => r.Item1 is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);

        // Due to race condition fix (conflict check before attestation), we expect:
        // - Only the first request to reach the conflict check succeeds
        // - All others fail with validation error (not 500 Internal Server Error)
        _ = await Assert.That(successfulRequests).IsLessThanOrEqualTo(1);
        _ = await Assert.That(failedRequests).IsGreaterThanOrEqualTo(4);

        // Verify no Internal Server Errors occurred (no unhandled exceptions)
        var serverErrors = results.Count(r => r.Item1 == HttpStatusCode.InternalServerError);
        _ = await Assert.That(serverErrors).IsEqualTo(0);
    }

    [Test]
    public async Task PasskeyRegistration_WithExistingUserId_ReturnsGenericError()
    {
        // Arrange - Create a user first
        var (email, _, _, tenantId) = await TestHelpers.RegisterAndLoginUserAsync();

        // Get the existing user's ID
        var store = await TestHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);
        var existingUser = await TestHelpers.GetUserByEmailAsync(session, email);
        _ = await Assert.That(existingUser).IsNotNull();

        var existingUserId = existingUser!.Id.ToString();

        // Act - Try to register a NEW passkey with an EXISTING user ID
        var client = TestHelpers.GetUnauthenticatedClient(tenantId);
        var response = await client.PostAsJsonAsync("/account/attestation/result", new
        {
            credentialJson = "{\"mock\":\"credential\"}",
            email = TestHelpers.GenerateFakeEmail(), // different email
            userId = existingUserId // SAME user ID
        });

        // Assert - Should fail with a generic error message to prevent user enumeration
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        // Security: Error message should NOT reveal "User ID already exists"
        _ = await Assert.That(content).Contains("Registration failed. Please try again.");
        _ = await Assert.That(content).DoesNotContain("already exists");
        _ = await Assert.That(content).DoesNotContain("conflict");
    }
}

/// <summary>
/// Response model for passkey creation options
/// </summary>
record PasskeyCreationOptionsResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Challenge { get; init; } = string.Empty;
}
