using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class DatabaseSeederPasswordTests
{
    [Test]
    [Category("Unit")]
    public async Task ResolveAdminSeedPassword_WithExplicitPassword_ShouldReturnExplicitPassword()
    {
        // Act
        var resolved = DatabaseSeeder.ResolveAdminSeedPassword(
            password: "Explicit-Password-123!",
            defaultPassword: "Configured-Password-123!",
            allowInsecureDevelopmentFallback: false);

        // Assert
        _ = await Assert.That(resolved).IsEqualTo("Explicit-Password-123!");
    }

    [Test]
    [Category("Unit")]
    public async Task ResolveAdminSeedPassword_WithoutExplicitPassword_WithConfiguredDefault_ShouldReturnConfiguredPassword()
    {
        // Act
        var resolved = DatabaseSeeder.ResolveAdminSeedPassword(
            password: null,
            defaultPassword: "Configured-Password-123!",
            allowInsecureDevelopmentFallback: false);

        // Assert
        _ = await Assert.That(resolved).IsEqualTo("Configured-Password-123!");
    }

    [Test]
    [Category("Unit")]
    public async Task ResolveAdminSeedPassword_WithoutPasswords_InDevelopment_ShouldReturnLegacyFallback()
    {
        // Act
        var resolved = DatabaseSeeder.ResolveAdminSeedPassword(
            password: null,
            defaultPassword: null,
            allowInsecureDevelopmentFallback: true);

        // Assert
        _ = await Assert.That(resolved).IsEqualTo("Admin123!");
    }

    [Test]
    [Category("Unit")]
    public async Task ResolveAdminSeedPassword_WithoutPasswords_OutsideDevelopment_ShouldThrow()
        // Act + Assert
        => _ = await Assert.That(() => DatabaseSeeder.ResolveAdminSeedPassword(
                password: null,
                defaultPassword: null,
                allowInsecureDevelopmentFallback: false))
            .Throws<InvalidOperationException>()
            .WithMessage("Tenant admin seeding requires an explicit password outside Development/Test. Provide the command password or configure Seeding:AdminPassword.");
}
