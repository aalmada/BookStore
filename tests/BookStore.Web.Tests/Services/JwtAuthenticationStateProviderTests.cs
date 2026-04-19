using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Services;

public class JwtAuthenticationStateProviderTests : BunitTestContext
{
    TokenService _tokenService = null!;
    ILocalStorageService _localStorage = null!;
    ITenantsClient _tenantClient = null!;
    IIdentityClient _identityClient = null!;
    IJSRuntime _jsRuntime = null!;
    ILogger<JwtAuthenticationStateProvider> _logger = null!;
    TenantService _tenantService = null!;
    JwtAuthenticationStateProvider _sut = null!;

    [Before(Test)]
    public void Setup()
    {
        _tokenService = new TokenService();
        _localStorage = Substitute.For<ILocalStorageService>();
        _tenantClient = Substitute.For<ITenantsClient>();
        _identityClient = Substitute.For<IIdentityClient>();
        _jsRuntime = Substitute.For<IJSRuntime>();
        _logger = Substitute.For<ILogger<JwtAuthenticationStateProvider>>();
        _ = _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var navigation = Context.Services.GetRequiredService<NavigationManager>();

        _tenantService = new TenantService(_tenantClient, navigation, _localStorage, _jsRuntime);
        _sut = new JwtAuthenticationStateProvider(
            _tokenService,
            _tenantService,
            _identityClient,
            _logger);
    }

    [After(Test)]
    public void Cleanup()
    {
        _sut.Dispose();
        _tenantService.Dispose();
    }

    [Test]
    [Category("Unit")]
    public async Task GetAuthenticationStateAsync_WhenNoTokenInMemory_ShouldReturnAnonymous()
    {
        // Act
        var state = await _sut.GetAuthenticationStateAsync();

        // Assert
        _ = await Assert.That(state.User.Identity?.IsAuthenticated ?? false).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task GetAuthenticationStateAsync_WhenTokenIsInvalid_ShouldClearTokensAndLog()
    {
        // Arrange
        var tenantId = _tenantService.CurrentTenantId;
        _tokenService.SetTokens(tenantId, "not-a-jwt", "refresh-token");

        // Act
        var state = await _sut.GetAuthenticationStateAsync();

        // Assert
        _ = await Assert.That(state.User.Identity?.IsAuthenticated ?? false).IsFalse();
        _ = await Assert.That(_tokenService.GetAccessToken(tenantId)).IsNull();

        var logCallCount = _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log));
        _ = await Assert.That(logCallCount).IsGreaterThan(0);
    }

    [Test]
    [Category("Unit")]
    public async Task GetAuthenticationStateAsync_WhenRefreshThrows_ShouldClearTokensAndLog()
    {
        // Arrange
        var tenantId = _tenantService.CurrentTenantId;
        var expiredToken = CreateJwtToken(tenantId, DateTime.UtcNow.AddMinutes(-30));
        _tokenService.SetTokens(tenantId, expiredToken, "refresh-token");

        _ = _identityClient.RefreshTokenAsync(Arg.Any<RefreshRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<LoginResponse>(new Exception("refresh failed")));

        // Act
        var state = await _sut.GetAuthenticationStateAsync();

        // Assert
        _ = await Assert.That(state.User.Identity?.IsAuthenticated ?? false).IsFalse();
        _ = await Assert.That(_tokenService.GetAccessToken(tenantId)).IsNull();

        var logCallCount = _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log));
        _ = await Assert.That(logCallCount).IsGreaterThan(0);
    }

    static string CreateJwtToken(string tenantId, DateTime expiresUtc)
    {
        var token = new JwtSecurityToken(
            claims: [new Claim("tenant_id", tenantId)],
            expires: expiresUtc);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
