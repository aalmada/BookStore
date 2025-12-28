var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL with pg_trgm extension for ngram search
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var bookStoreDb = postgres.AddDatabase("bookstore");

// Add Azure Storage with Azurite emulator for local development
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(); // Runs Azurite container automatically

var blobs = storage.AddBlobs("blobs");

var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(bookStoreDb)
    .WithReference(blobs) // Add blob storage reference
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "API Reference";
        url.Url += "/api-reference";
    })
    .WaitFor(postgres);

builder.AddProject<Projects.BookStore_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
