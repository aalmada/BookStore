using System.Text.Json;
using Bogus;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using TUnit.Core.Interfaces;

namespace BookStore.AppHost.Tests;

public class PasskeyTests
{
    readonly IIdentityClient _identityClient;
    readonly IPasskeyClient _passkeyClient;
    readonly Faker _faker;

    public PasskeyTests()
    {
        var httpClient = HttpClientHelpers.GetUnauthenticatedClient();
        _identityClient = RestService.For<IIdentityClient>(httpClient);
        _passkeyClient = RestService.For<IPasskeyClient>(httpClient);
        _faker = new Faker();
    }

    [Test]
    public async Task GetAssertionOptions_WithUserWithNoPasskeys_ShouldReturnOptions()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        var registerResponse = await _identityClient.RegisterAsync(new RegisterRequest(email, password));
        _ = await Assert.That(registerResponse).IsNotNull();

        var request = new PasskeyLoginOptionsRequest { Email = email };

        // Act
        var options = await _passkeyClient.GetPasskeyLoginOptionsAsync(request);

        // Assert - API returns options even for users without passkeys to prevent user enumeration
        _ = await Assert.That(options).IsNotNull();
    }

    [Test]
    public async Task GetAttestationOptions_WithExistingUser_ShouldReturnOk()
    {
        // Arrange
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register a user
        _ = await _identityClient.RegisterAsync(new RegisterRequest(email, password));

        // Act - Try to start passkey registration for same email (unauthenticated flow)
        var response =
            await _passkeyClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest { Email = email });

        // Assert - Should return OK to prevent enumeration (currently fails with BadRequest)
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Options).IsNotNull();
    }

    [Test]
    public async Task GetAttestationOptions_WhenAuthenticated_ShouldReturnOptions()
    {
        // Arrange - Need to be logged in to register a passkey
        var email = _faker.Internet.Email();
        var password = _faker.Internet.Password(8, false, "\\w", "Aa1!");

        // Register
        _ = await _identityClient.RegisterAsync(new RegisterRequest(email, password));

        // Login
        var loginResult = await _identityClient.LoginAsync(new LoginRequest(email, password));

        // Create authenticated client
        var authHttpClient = HttpClientHelpers.GetUnauthenticatedClient();
        authHttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        var authPasskeyClient = RestService.For<IPasskeyClient>(authHttpClient);

        // Act
        // This endpoint usually doesn't need a body if the user is already authenticated
        // It uses the identity from the claims to generate options for that user
        var response = await authPasskeyClient.GetPasskeyCreationOptionsAsync(new PasskeyCreationRequest());

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Options).IsNotNull(); // Options is object

        // To verify deeply we need to inspect the object (JObject/JsonElement)
        var optionsJson = JsonSerializer.Serialize(response.Options);
        var optionsDoc = JsonDocument.Parse(optionsJson);
        var root = optionsDoc.RootElement;

        _ = await Assert.That(root.GetProperty("challenge").GetString()).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(root.GetProperty("user").GetProperty("name").GetString()).IsEqualTo(email);

        _ = await Assert.That(response.UserId).IsNotNull().And.IsNotEmpty();
    }
}
