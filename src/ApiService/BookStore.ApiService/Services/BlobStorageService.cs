using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BookStore.ApiService.Services;

public class BlobStorageService(BlobServiceClient blobServiceClient)
{
    const string ContainerName = "book-covers";
    static readonly string[] SupportedExtensions = ["jpg", "png", "webp"];

    public async Task<string> UploadBookCoverAsync(
        Guid bookId,
        Stream imageStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        
        // Determine file extension from content type
        var extension = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "jpg" // Default to jpg for safety
        };
        
        var blobName = $"{bookId}.{extension}";
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(
            imageStream,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken);

        return blob.Uri.ToString();
    }

    public async Task<BlobDownloadResult> GetBookCoverAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        
        // Try to find the blob with any supported extension
        foreach (var ext in SupportedExtensions)
        {
            var blob = container.GetBlobClient($"{bookId}.{ext}");
            if (await blob.ExistsAsync(cancellationToken))
            {
                return await blob.DownloadContentAsync(cancellationToken);
            }
        }
        
        throw new FileNotFoundException($"Book cover not found for book {bookId}");
    }

    public async Task DeleteBookCoverAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        
        // Delete blob with any supported extension
        foreach (var ext in SupportedExtensions)
        {
            var blob = container.GetBlobClient($"{bookId}.{ext}");
            await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    async Task<BlobContainerClient> GetContainerAsync(
        CancellationToken cancellationToken = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(
            PublicAccessType.Blob,
            cancellationToken: cancellationToken);
        return container;
    }
}
