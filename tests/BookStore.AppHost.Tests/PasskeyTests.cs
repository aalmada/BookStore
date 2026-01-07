using System.Net;
using System.Net.Http.Json;
using Bogus;
using BookStore.AppHost.Tests;
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
    public async Task GetAssertionOptions_WithValidUsername_ShouldReturnOptions()
    {
        // Arrange
        // We generally need a registered user to get assurance options, 
        // OR the endpoint might accept any username depending on implementation 
        // (usually checking if user exists first).
        // Let's create a user first to be sure.
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        var registerResponse = await _client.PostAsJsonAsync("/account/register", new { Email = email, Password = password });
        _ = await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var request = new { Username = email };

        // Act
        var response = await _client.PostAsJsonAsync("/account/assertion/options", request);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var options = await response.Content.ReadFromJsonAsync<AssertionOptions>();
        _ = await Assert.That(options).IsNotNull();
        _ = await Assert.That(options!.Challenge).IsNotNull().And.IsNotEmpty();
        // AllowCredentials might be empty if no passkeys registered yet, but the object should exist
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
        authClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        // Act
        // This endpoint usually doesn't need a body if the user is already authenticated
        // It uses the identity from the claims to generate options for that user
        var response = await authClient.PostAsJsonAsync("/account/attestation/options", new { });

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var options = await response.Content.ReadFromJsonAsync<AttestationOptions>();
        _ = await Assert.That(options).IsNotNull();
        _ = await Assert.That(options!.Challenge).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(options.User.Name).IsEqualTo(email);
    }

    // Minimal records for deserialization
    record LoginResponse(string AccessToken, string RefreshToken);
    record AssertionOptions(string Challenge);
    record AttestationOptions(string Challenge, UserEntity User);
    record UserEntity(string Name, string Id, string DisplayName);
}
