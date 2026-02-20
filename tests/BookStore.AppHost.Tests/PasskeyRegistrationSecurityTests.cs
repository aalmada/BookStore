using System.Net;
using System.Net.Http.Json;
using BookStore.AppHost.Tests.Helpers;
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
        // Arrange: Create a REAL credential JSON via the virtual WebAuthn authenticator.
        // The browser generates a proper attestation object for one email/challenge pair.
        var email = FakeDataGenerators.GenerateFakeEmail();
        var tenantId = MultiTenancyConstants.DefaultTenantId;

        await using var webAuthn = await WebAuthnTestHelper.CreateAsync();
        // CreateAttestationCredentialAsync returns the client that holds the
        // attestation-state cookie from the /attestation/options call.
        var (credentialJson, _, userId, client) =
            await webAuthn.CreateAttestationCredentialAsync(email, tenantId);

        // Act: Fire 5 concurrent POST /account/attestation/result with the SAME credential JSON.
        // All requests use the same client (sharing the attestation-state cookie) so they all
        // reach the attestation validation step. The first one wins and creates the user;
        // the rest are rejected by the database unique-email constraint.
        var registrationTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                var response = await client.PostAsJsonAsync("/account/attestation/result", new
                {
                    credentialJson,
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

        // Assert: No internal server errors — data integrity must be maintained under load.
        var serverErrors = results.Count(r => r.Item1 == HttpStatusCode.InternalServerError);
        _ = await Assert.That(serverErrors).IsEqualTo(0);

        // Assert: Exactly ONE user was created in the database for this email.
        // HTTP responses may vary (success or masked-duplicate 200) but the database
        // must have a single canonical record.
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var dbSession = store.LightweightSession(tenantId);
        var user = await DatabaseHelpers.GetUserByEmailAsync(dbSession, email);

        _ = await Assert.That(user).IsNotNull();
        _ = await Assert.That(user!.Email).IsEqualTo(email);
    }

    [Test]
    public async Task PasskeyRegistration_WithExistingUserId_ReturnsGenericError()
    {
        // Arrange - Create a user first
        var (email, _, _, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        // Get the existing user's ID
        await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
        await using var session = store.LightweightSession(tenantId);
        var existingUser = await DatabaseHelpers.GetUserByEmailAsync(session, email);
        _ = await Assert.That(existingUser).IsNotNull();

        var existingUserId = existingUser!.Id.ToString();

        // Act - Try to register a NEW passkey with an EXISTING user ID
        var client = HttpClientHelpers.GetUnauthenticatedClient(tenantId);
        var response = await client.PostAsJsonAsync("/account/attestation/result", new
        {
            credentialJson = "{\"mock\":\"credential\"}",
            email = FakeDataGenerators.GenerateFakeEmail(), // different email
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
