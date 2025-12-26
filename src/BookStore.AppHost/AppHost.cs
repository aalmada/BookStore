var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL with pg_trgm extension for ngram search
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var bookStoreDb = postgres.AddDatabase("bookstore");

var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(bookStoreDb)
    .WithHttpHealthCheck("/health")
    .WaitFor(postgres);

builder.AddProject<Projects.BookStore_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
