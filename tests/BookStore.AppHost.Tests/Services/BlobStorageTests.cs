using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BookStore.ApiService.Services;
using BookStore.ServiceDefaults;
using JasperFx;
using Microsoft.Extensions.Hosting;

namespace BookStore.AppHost.Tests.Services;

public class BlobStorageTests
{
    BlobStorageService? _blobStorageService;
    BlobServiceClient? _blobServiceClient;
    const string ContainerName = "book-covers";

    [Before(Test)]
    public async Task Setup()
    {
        var app = GlobalHooks.App ?? throw new InvalidOperationException("App is not initialized");

        // The resource name in AppHost is "blobs" (ResourceNames.Blobs) not "storage"
        var connectionString = await app.GetConnectionStringAsync(ResourceNames.Blobs);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not retrieve connection string for 'blobs' resource.");
        }

        _blobServiceClient = new BlobServiceClient(connectionString);
        _blobStorageService = new BlobStorageService(_blobServiceClient);

        // Ensure container exists for tests
        var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
        _ = await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        if (_blobServiceClient != null)
        {
            // Clean up the container after each test
            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            _ = await container.DeleteIfExistsAsync();
        }
    }

    [Test]
    [Category("Integration")]
    public async Task UploadBookCoverAsync_ShouldUploadAndReturnUri()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var content = new byte[] { 0x1, 0x2, 0x3, 0x4 };
        using var stream = new MemoryStream(content);
        var contentType = "image/jpeg";

        // Act
        var uri = await _blobStorageService!.UploadBookCoverAsync(bookId, stream, contentType,
            StorageConstants.DefaultTenantId);

        // Assert
        _ = await Assert.That(uri).IsNotNull();
        _ = await Assert.That(uri).Contains(bookId.ToString());
        _ = await Assert.That(uri).EndsWith(".jpg");

        // Verify blob exists
        var container = _blobServiceClient!.GetBlobContainerClient(ContainerName);
        var blob = container.GetBlobClient($"{StorageConstants.DefaultTenantId}/{bookId}.jpg");
        var exists = await blob.ExistsAsync();
        _ = await Assert.That(exists.Value).IsTrue();
    }

    [Test]
    [Category("Integration")]
    public async Task GetBookCoverAsync_ShouldRetrieveContent()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var content = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        using var stream = new MemoryStream(content);
        var contentType = "image/png";

        // Upload first
        _ = await _blobStorageService!.UploadBookCoverAsync(bookId, stream, contentType,
            StorageConstants.DefaultTenantId);

        // Act
        var result = await _blobStorageService.GetBookCoverAsync(bookId);

        // Assert
        var downloadContent = result.Content.ToMemory();
        _ = await Assert.That(downloadContent.ToArray()).IsEquivalentTo(content);
    }

    [Test]
    [Category("Integration")]
    public async Task GetBookCoverAsync_WhenBookDoesNotExist_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        // Act & Assert
        _ = await Assert.That(async () => await _blobStorageService!.GetBookCoverAsync(bookId))
            .Throws<FileNotFoundException>();
    }

    [Test]
    [Category("Integration")]
    public async Task DeleteBookCoverAsync_ShouldRemoveBlob()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var content = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        using var stream = new MemoryStream(content);
        var contentType = "image/webp";

        _ = await _blobStorageService!.UploadBookCoverAsync(bookId, stream, contentType,
            StorageConstants.DefaultTenantId);

        // Verify existence before delete
        var container = _blobServiceClient!.GetBlobContainerClient(ContainerName);
        var blob = container.GetBlobClient($"{StorageConstants.DefaultTenantId}/{bookId}.webp");
        var existsBefore = await blob.ExistsAsync();
        _ = await Assert.That(existsBefore.Value).IsTrue();

        // Act
        await _blobStorageService.DeleteBookCoverAsync(bookId);

        // Assert
        var existsAfter = await blob.ExistsAsync();
        _ = await Assert.That(existsAfter.Value).IsFalse();
    }
}
