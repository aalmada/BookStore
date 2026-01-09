using BookStore.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL with pg_trgm extension for ngram search
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithPgAdmin();
// .WithDataVolume();

var bookStoreDb = postgres.AddDatabase(ResourceNames.BookStoreDb);

// Add Azure Storage with Azurite emulator for local development
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(); // Runs Azurite container automatically

var blobs = storage.AddBlobs(ResourceNames.Blobs);

var cache = builder.AddRedis(ResourceNames.Cache);

var apiService = builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(bookStoreDb)
    .WithReference(blobs) // Add blob storage reference
    .WithReference(cache)
    .WithHttpHealthCheck(ResourceNames.HealthCheckEndpoint)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = ResourceNames.ApiReferenceText;
        url.Url += ResourceNames.ApiReferenceUrl;
    })
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = ResourceNames.ApiReferenceText;
        url.Url += ResourceNames.ApiReferenceUrl;
    })
    .WaitFor(postgres);

builder.AddProject<Projects.BookStore_Web>(ResourceNames.WebFrontend)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck(ResourceNames.HealthCheckEndpoint)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();

