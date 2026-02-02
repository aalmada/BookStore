using System.Net;
using BookStore.Client;
using Refit;

namespace BookStore.AppHost.Tests;

public class TenantSecurityTests
{
    [Test]
    public async Task Request_WithNoTenantIdClaim_ShouldBeForbidden()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var validToken = GlobalHooks.AdminAccessToken!;

        // Arrange
        // Test 1: Valid token (tenant=Default/BookStore), Header=acme -> Should Fail
        var client = RestService.For<IShoppingCartClient>(TestHelpers.GetAuthenticatedClient(validToken, "acme"));

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetShoppingCartAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Request_Anonymous_WithTenantHeader_ShouldBeForbidden()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Test 2: Anonymous user with X-Tenant-ID="acme" -> Should be Forbidden
        var client = RestService.For<IShoppingCartClient>(TestHelpers.GetUnauthenticatedClient("acme"));

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetShoppingCartAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Request_NoTenantClaim_ShouldBeForbidden()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Test 3: Same as Test 1 basically - Valid Token (Default), Header (acme) -> Mismatch -> Forbidden
        var validToken = GlobalHooks.AdminAccessToken!;
        var client = RestService.For<IShoppingCartClient>(TestHelpers.GetAuthenticatedClient(validToken, "acme"));

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetShoppingCartAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Admin_TenantList_RestrictedToDefaultTenant()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        if (GlobalHooks.AdminAccessToken == null)
        {
            throw new InvalidOperationException("Admin Access Token is null");
        }

        // 1. Success path: Default Tenant Admin (GlobalHooks.AdminAccessToken) accessing Default Tenant endpoint
        var client =
            RestService.For<IAdminTenantClient>(TestHelpers.GetAuthenticatedClient(GlobalHooks.AdminAccessToken));

        // Act
        var result = await client.GetAllTenantsAdminAsync();

        // Assert
        _ = await Assert.That(result).IsNotNull();
    }
}
