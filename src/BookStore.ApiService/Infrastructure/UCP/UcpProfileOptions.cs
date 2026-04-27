namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpProfileOptions
{
    public const string SectionName = "Ucp";

    public string ServiceBaseUrl { get; init; } = string.Empty;
    public string SigningKeyId { get; init; } = "bookstore-key-2026";
    public string? SigningPublicKeyBase64 { get; init; }
    public string? SigningPrivateKeyBase64 { get; init; }
    public bool RequireSignatures { get; init; }
}
