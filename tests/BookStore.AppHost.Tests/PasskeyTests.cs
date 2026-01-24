using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.AppHost.Tests;
using BookStore.Shared.Models;
using TUnit.Core.Interfaces;

namespace BookStore.AppHost.Tests;

public class PasskeyTests
{
    readonly HttpClient _client;
    readonly Faker _faker;

    public PasskeyTests()
    {
        _client = TestHelpers.GetUnauthenticatedClient();
        _faker = new Faker();
    }

    [Test]
    public async Task GetAssertionOptions_WithUserWithNoPasskeys_ShouldReturnBadRequest()
    {
        // Arrange
        // A user registered with password only (no passkeys) should get BadRequest
        // when trying to get assertion options (passkey login options)
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        var registerResponse =
            await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var request = new { Email = email };

        // Act
        var response = await _client.PostAsJsonAsync("/account/assertion/options", request);

        // Assert - Should return BadRequest because user has no passkeys registered
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<TestHelpers.ErrorResponse>();
        _ = await Assert.That(error?.Error).IsEqualTo(ErrorCodes.Passkey.UserNotFound);
    }

    [Test]
    public async Task GetAttestationOptions_WithExistingUser_ShouldReturnOk()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register a user
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        // Act - Try to start passkey registration for same email (unauthenticated flow)
        var response = await _client.PostAsJsonAsync("/account/attestation/options", new { Email = email });

        // Assert - Should return OK to prevent enumeration (currently fails with BadRequest)
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify it returns options structure
        var wrappedOptions = await response.Content.ReadFromJsonAsync<AttestationOptionsResponse>();
        _ = await Assert.That(wrappedOptions).IsNotNull();
        _ = await Assert.That(wrappedOptions!.Options).IsNotNull();
    }

    [Test]
    public async Task GetAttestationOptions_WhenAuthenticated_ShouldReturnOptions()
    {
        // Arrange - Need to be logged in to register a passkey
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        _ = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });

        // Login
        var loginResponse = await _client.PostAsJsonAsync("/account/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create authenticated client
        // Use a fresh client to avoid header pollution or just use common helper and set header
        var authClient = TestHelpers.GetUnauthenticatedClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        // Act
        // This endpoint usually doesn't need a body if the user is already authenticated
        // It uses the identity from the claims to generate options for that user
        var response = await authClient.PostAsJsonAsync("/account/attestation/options", new { });

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Response is now wrapped: { options: {...}, userId: "..." }
        var wrappedOptions = await response.Content.ReadFromJsonAsync<AttestationOptionsResponse>();
        _ = await Assert.That(wrappedOptions).IsNotNull();
        _ = await Assert.That(wrappedOptions!.Options).IsNotNull();
        _ = await Assert.That(wrappedOptions.Options!.Challenge).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(wrappedOptions.Options.User.Name).IsEqualTo(email);
        _ = await Assert.That(wrappedOptions.UserId).IsNotNull().And.IsNotEmpty();
    }

    // Minimal records for deserialization
    record LoginResponse(string AccessToken, string RefreshToken);

    record AssertionOptions(string Challenge);

    record AttestationOptions(string Challenge, UserEntity User);

    record AttestationOptionsResponse(AttestationOptions Options, string UserId);

    record UserEntity(string Name, string Id, string DisplayName);
}
