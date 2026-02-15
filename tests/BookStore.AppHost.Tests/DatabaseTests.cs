using Aspire.Hosting;
using BookStore.AppHost.Tests.Helpers;
using Npgsql;

namespace BookStore.AppHost.Tests;

public class DatabaseTests
{
    [Test]
    public async Task CanConnectToDatabase()
    {
        // Arrange
        var app = GlobalHooks.App!;
        var notificationService = GlobalHooks.NotificationService!;

        _ = await notificationService.WaitForResourceHealthyAsync("postgres", CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        var connectionString = await app.GetConnectionStringAsync("postgres", CancellationToken.None);

        _ = await Assert.That(connectionString).IsNotNull();

        await using var connection = new NpgsqlConnection(connectionString);

        // Act
        await connection.OpenAsync(CancellationToken.None).WaitAsync(TestConstants.DefaultTimeout);

        // Assert
        _ = await Assert.That(connection.State).IsEqualTo(System.Data.ConnectionState.Open);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        var result = await command.ExecuteScalarAsync(CancellationToken.None);

        _ = await Assert.That(result).IsEqualTo(1);
    }
}
