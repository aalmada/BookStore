using Blazored.LocalStorage;
using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Web.Services;
using BookStore.Web.Tests.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;
using TUnit.Core;

namespace BookStore.Web.Tests.Services;

public class TenantServiceTests : BunitTestContext
{
    ITenantClient _tenantClient = null!;
    ILocalStorageService _localStorage = null!;
    IJSRuntime _js = null!;
    TenantService _sut = null!;

    [Before(Test)]
    public void Setup()
    {
        _tenantClient = Substitute.For<ITenantClient>();
        _localStorage = Substitute.For<ILocalStorageService>();
        _js = Substitute.For<IJSRuntime>();

        // NavigationManager is already registered in bUnit Context.Services as FakeNavigationManager
        var navigation = Context.Services.GetRequiredService<NavigationManager>();

        _sut = new TenantService(_tenantClient, navigation, _localStorage, _js);
    }

    [After(Test)]
    public void Cleanup() => _sut.Dispose();

    [Test]
    public async Task InitializeAsync_ShouldSetTenantFromUrl_WhenPresent()
    {
        // Arrange
        var navigation = Context.Services.GetRequiredService<FakeNavigationManager>();
        navigation.NavigateTo("http://localhost/?tenant=apple");

        var tenantInfo = new TenantInfoDto("apple", "Apple Store", "Tagline", "#FFFFFF", true);
        _ = _tenantClient.GetTenantAsync("apple").Returns(tenantInfo);
        _ = _tenantClient.GetTenantsAsync().Returns([tenantInfo]);

        // Act
        await _sut.InitializeAsync();

        // Assert
        _ = await Assert.That(_sut.CurrentTenantId).IsEqualTo("apple");
        _ = await Assert.That(_sut.CurrentTenantName).IsEqualTo("Apple Store");
    }

    [Test]
    public async Task InitializeAsync_ShouldFallbackToLocalStorage_WhenUrlEmpty()
    {
        // Arrange
        var navigation = Context.Services.GetRequiredService<FakeNavigationManager>();
        navigation.NavigateTo("http://localhost/");

        _ = _localStorage.GetItemAsStringAsync("selected-tenant").Returns("banana");
        var tenantInfo = new TenantInfoDto("banana", "Banana Store", "Tagline", "#FFFF00", true);
        _ = _tenantClient.GetTenantAsync("banana").Returns(tenantInfo);
        _ = _tenantClient.GetTenantsAsync().Returns([tenantInfo]);

        // Act
        await _sut.InitializeAsync();

        // Assert
        _ = await Assert.That(_sut.CurrentTenantId).IsEqualTo("banana");
    }

    [Test]
    public async Task SetTenantAsync_ShouldNotifyOnChange_AndSaveToStorage()
    {
        // Arrange
        var tenantInfo = new TenantInfoDto("cherry", "Cherry Store", "Tagline", "#FF0000", true);
        _ = _tenantClient.GetTenantAsync("cherry").Returns(tenantInfo);
        var onChangeCalled = false;
        _sut.OnChange += () => onChangeCalled = true;

        // Act
        _ = await _sut.SetTenantAsync("cherry");

        // Assert
        _ = await Assert.That(_sut.CurrentTenantId).IsEqualTo("cherry");
        _ = await Assert.That(onChangeCalled).IsTrue();
        await _localStorage.Received(1).SetItemAsStringAsync("selected-tenant", "cherry");
    }

    [Test]
    public async Task SetTenantAsync_ShouldFallbackToDefault_OnError()
    {
        // Arrange
        _ = _tenantClient.GetTenantAsync("invalid").Returns(Task.FromException<TenantInfoDto>(new Exception("Not found")));

        var defaultInfo = new TenantInfoDto("default", "Default", null, null, true);
        _ = _tenantClient.GetTenantAsync(Arg.Any<string>()).Returns(x =>
        {
            if ((string)x[0] == "invalid")
            {
                throw new Exception("Not found");
            }

            return defaultInfo;
        });

        // Act
        _ = await _sut.SetTenantAsync("invalid");

        // Assert
        _ = await Assert.That(_sut.CurrentTenantId).IsNotEqualTo("invalid");
    }
}
